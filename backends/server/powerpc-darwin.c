#include <server.h>
#include <sys/types.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <string.h>
#include <breakpoints.h>

#define DEBUG_WAIT 0

#include "powerpc-arch.h"
#include "powerpc-darwin.h"

struct InferiorHandle {
	int pid;
	task_t task;
	thread_t thread;
	int pagesize;
	int last_signal;
};

static ServerHandle *
powerpc_initialize (BreakpointManager *bpm)
{
	ServerHandle *handle = g_new0 (ServerHandle, 1);

	handle->bpm = bpm;
	handle->inferior = g_new0 (InferiorHandle, 1);
	handle->arch = powerpc_arch_initialize ();
	return handle;
}

static int global_pid = 0;
static int stop_requested = 0;
static int stop_status = 0;

static int
do_wait (int pid, guint32 *status)
{
	int ret;

#ifdef DEBUG_WAIT
	g_message (G_STRLOC ": Calling waitpid (%d)", pid);
#endif
	ret = waitpid (pid, status, WUNTRACED);
#ifdef DEBUG_WAIT
	g_message (G_STRLOC ": waitpid (%d) returned %d - %x", pid, ret, *status);
#endif
	if (ret < 0) {
		if (errno == EINTR)
			return 0;
		g_warning (G_STRLOC ": Can't waitpid for %d: %s", pid, g_strerror (errno));
		return -1;
	}

	return ret;
}

static int first_status = 0;
static int first_ret = 0;

static void
_powerpc_setup_inferior (ServerHandle *handle, gboolean is_main)
{
	int status, ret;
	kern_return_t kret;
	mach_port_t itask;
	thread_array_t thread_list;
	unsigned int count;

	kret = task_for_pid (mach_task_self(), handle->inferior->pid, &itask);
	g_assert (kret == KERN_SUCCESS);
	handle->inferior->task = itask;

	kret = task_threads (itask, &thread_list, &count);
	g_assert ((kret == KERN_SUCCESS) && (count >= 1));

	handle->inferior->thread = thread_list [0];

	kret = host_page_size (mach_host_self (), &handle->inferior->pagesize);
	g_assert (kret == KERN_SUCCESS);

	do {
		ret = do_wait (handle->inferior->pid, &status);
	} while (ret == 0);

	if (is_main) {
		g_assert (ret == handle->inferior->pid);
		first_status = status;
		first_ret = ret;
		global_pid = handle->inferior->pid;
	}

	powerpc_arch_get_registers (handle);
}

static void
child_setup_func (gpointer data)
{
	if (ptrace (PT_TRACE_ME, getpid (), NULL, 0))
		g_error (G_STRLOC ": Can't PT_TRACEME: %s", g_strerror (errno));
}

static ServerCommandError
powerpc_spawn (ServerHandle *handle, const gchar *working_directory,
	       const gchar **argv, const gchar **envp, gint *child_pid,
	       ChildOutputFunc stdout_handler, ChildOutputFunc stderr_handler,
	       gchar **error)
{
	InferiorHandle *inferior = handle->inferior;
	int fd[2], open_max, ret, len, i;

	*error = NULL;

	pipe (fd);

	*child_pid = fork ();
	if (*child_pid == 0) {
		gchar *error_message;

		open_max = sysconf (_SC_OPEN_MAX);
		for (i = 3; i < open_max; i++)
			fcntl (i, F_SETFD, FD_CLOEXEC);

		setsid ();

		child_setup_func (NULL);
		execve (argv [0], (char **) argv, (char **) envp);

		error_message = g_strdup_printf ("Cannot exec `%s': %s", argv [0], g_strerror (errno));
		len = strlen (error_message) + 1;
		write (fd [1], &len, sizeof (len));
		write (fd [1], error_message, len);
		_exit (1);
	}

	close (fd [1]);
	ret = read (fd [0], &len, sizeof (len));

	if (ret != 0) {
		g_assert (ret == 4);

		*error = g_malloc0 (len);
		read (fd [0], *error, len);
		close (fd [0]);
		return COMMAND_ERROR_FORK;
	}

	inferior->pid = *child_pid;
	_powerpc_setup_inferior (handle, TRUE);

	return COMMAND_ERROR_NONE;
}

static ServerCommandError
powerpc_get_target_info (guint32 *target_int_size, guint32 *target_long_size,
			 guint32 *target_address_size)
{
	*target_int_size = sizeof (guint32);
	*target_long_size = sizeof (guint64);
	*target_address_size = sizeof (void *);

	return COMMAND_ERROR_NONE;
}

GStaticMutex wait_mutex = G_STATIC_MUTEX_INIT;
GStaticMutex wait_mutex_2 = G_STATIC_MUTEX_INIT;

static guint32
powerpc_global_wait (guint32 *status_ret)
{
	int ret, status;

	if (first_status) {
		*status_ret = first_status;
		first_status = 0;
		return first_ret;
	}

	g_static_mutex_lock (&wait_mutex);
	ret = do_wait (-1, &status);
	if (ret <= 0)
		goto out;

#if DEBUG_WAIT
	g_message (G_STRLOC ": global wait finished: %d - %x - %d",
		   ret, status, stop_requested);
#endif

	g_static_mutex_lock (&wait_mutex_2);
	if (ret == stop_requested) {
		stop_status = status;
		g_static_mutex_unlock (&wait_mutex_2);
		g_static_mutex_unlock (&wait_mutex);
		return 0;
	}
	g_static_mutex_unlock (&wait_mutex_2);

	*status_ret = status;
 out:
	g_static_mutex_unlock (&wait_mutex);
	return ret;
}

static ServerStatusMessageType
powerpc_dispatch_event (ServerHandle *handle, guint32 status, guint64 *arg,
			guint64 *data1, guint64 *data2)
{
	*arg = *data1 = *data2 = 0;

	if (WIFSTOPPED (status)) {
		powerpc_arch_get_registers (handle);

		if (WSTOPSIG (status) == SIGTRAP) {
			handle->inferior->last_signal = 0;
			*arg = 0;
		} else {
			if (WSTOPSIG (status) == SIGSTOP)
				handle->inferior->last_signal = 0;
			else
				handle->inferior->last_signal = WSTOPSIG (status);
			*arg = handle->inferior->last_signal;
		}
		return MESSAGE_CHILD_STOPPED;
	} else if (WIFEXITED (status)) {
		*arg = WEXITSTATUS (status);
		return MESSAGE_CHILD_EXITED;
	} else if (WIFSIGNALED (status)) {
		*arg = WTERMSIG (status);
		return MESSAGE_CHILD_SIGNALED;
	}

	g_warning (G_STRLOC ": Got unknown waitpid() result: %x", status);
	return MESSAGE_UNKNOWN_ERROR;
}

ServerCommandError
_powerpc_get_registers (InferiorHandle *inferior, INFERIOR_REGS_TYPE *regs)
{
	int count = PPC_THREAD_STATE_COUNT;
	kern_return_t kret;

	kret = thread_get_state (
		inferior->thread, PPC_THREAD_STATE, (thread_state_t) regs, &count);

	if (kret != KERN_SUCCESS) {
		g_warning (G_STRLOC ": thread_get_state(%d) returned %x (%s)",
			   inferior->thread, kret, mach_error_string (kret));
		return COMMAND_ERROR_UNKNOWN;
	}

	return COMMAND_ERROR_NONE;
}

static ServerCommandError
powerpc_continue (ServerHandle *handle)
{
	InferiorHandle *inferior = handle->inferior;

	errno = 0;
	if (ptrace (PT_CONTINUE, inferior->pid, (caddr_t) 1, inferior->last_signal)) {
		if (errno == ESRCH)
			return COMMAND_ERROR_NOT_STOPPED;

		return COMMAND_ERROR_UNKNOWN;
	}

	return COMMAND_ERROR_NONE;
}

static ServerCommandError
powerpc_step (ServerHandle *handle)
{
	InferiorHandle *inferior = handle->inferior;

	errno = 0;
	if (ptrace (PT_STEP, inferior->pid, (caddr_t) 1, inferior->last_signal)) {
		if (errno == ESRCH)
			return COMMAND_ERROR_NOT_STOPPED;

		return COMMAND_ERROR_UNKNOWN;
	}

	return COMMAND_ERROR_NONE;
}

static ServerCommandError
read_memory_remainder (InferiorHandle *inferior, vm_address_t addr, int size, gpointer buffer)
{
	kern_return_t kret;
	vm_offset_t data;
	int offset, count;

	offset = addr % inferior->pagesize;
	addr -= offset;

	kret = vm_read (inferior->task, addr, inferior->pagesize, &data, &count);
	if ((kret != KERN_SUCCESS) || (count != inferior->pagesize)) {
		g_warning (G_STRLOC ": Can't read target memory at %x: %x (%s)",
			   addr, kret, mach_error_string (kret));
		return COMMAND_ERROR_MEMORY_ACCESS;
	}

	memcpy (buffer, data + offset, size);
	return COMMAND_ERROR_NONE;
}

static ServerCommandError
powerpc_read_memory (ServerHandle *handle, guint64 start, guint32 size, gpointer buffer)
{
	unsigned int mempointer;
	vm_address_t addr = (guint32) start;
	kern_return_t kret;
	int count;

	return read_memory_remainder (handle->inferior, (vm_address_t) addr, size, buffer);
}

#include "powerpc-arch.c"

InferiorVTable powerpc_darwin_inferior = {
	powerpc_initialize,
	powerpc_spawn,
	NULL, // powerpc_attach,
	NULL, // powerpc_detach,
	NULL, // powerpc_finalize,
	powerpc_global_wait,
	NULL, // powerpc_stop_and_wait,
	powerpc_dispatch_event,
	powerpc_get_target_info,
	powerpc_continue,
	powerpc_step,
	powerpc_get_pc,
	NULL, // powerpc_current_insn_is_bpt,
	NULL, // powerpc_peek_word,
	powerpc_read_memory,
	NULL, // powerpc_write_memory,
	NULL, // powerpc_call_method,
	NULL, // powerpc_call_method_1,
	NULL, // powerpc_call_method_invoke,
	NULL, // powerpc_insert_breakpoint,
	NULL, // powerpc_insert_hw_breakpoint,
	NULL, // powerpc_remove_breakpoint,
	NULL, // powerpc_enable_breakpoint,
	NULL, // powerpc_disable_breakpoint,
	NULL, // powerpc_get_breakpoints,
	NULL, // powerpc_get_registers,
	NULL, // powerpc_set_registers,
	NULL, // powerpc_get_backtrace,
	powerpc_get_ret_address,
	NULL, // powerpc_stop,
	NULL, // powerpc_global_stop,
	NULL, // powerpc_set_signal,
	NULL, // powerpc_kill,
	NULL // powerpc_get_signal_info
};
