#include <glib.h>
#include <stdio.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <unistd.h>
#include <errno.h>

#include <server.h>

static int command_available = 0;
static int shutdown = 0;

static void
command_func (InferiorHandle *handle, int fd)
{
	ServerCommand command;
	ServerCommandError result;
	guint64 arg;

	if (read (fd, &command, sizeof (command)) != sizeof (command))
		g_error (G_STRLOC ": Can't read command: %s", g_strerror (errno));

	switch (command) {
	case SERVER_COMMAND_GET_PC:
		result = server_get_program_counter (handle, &arg);
		break;

	case SERVER_COMMAND_CONTINUE:
		result = server_ptrace_continue (handle);
		break;

	case SERVER_COMMAND_DETACH:
		result = server_ptrace_detach (handle);
		break;

	case SERVER_COMMAND_SHUTDOWN:
		shutdown = 1;
		result = COMMAND_ERROR_NONE;
		break;

	case SERVER_COMMAND_KILL:
		shutdown = 2;
		result = COMMAND_ERROR_NONE;
		break;

	default:
		result = COMMAND_ERROR_INVALID_COMMAND;
		break;
	}

	if (write (fd, &result, sizeof (result)) != sizeof (result))
		g_error (G_STRLOC ": Can't send command status: %s", g_strerror (errno));
}

static void
send_status_message (GIOChannel *channel, ServerStatusMessageType type, int arg)
{
	ServerStatusMessage message = { type, arg };
	GError *error = NULL;
	GIOStatus status;

	status = g_io_channel_write_chars (channel, (char *) &message, sizeof (message), NULL, &error);
	if (status != G_IO_STATUS_NORMAL)
		g_error (G_STRLOC ": Can't send status message: %s", error->message);
}

static void
child_setup_func (gpointer data)
{
	server_ptrace_traceme (getpid ());
}

#define usage() g_error (G_STRLOC ": This program must not be called directly.");

static void
command_signal_handler (int dummy)
{
	command_available = TRUE;
}

static void
signal_handler (int dummy)
{
	/* Do nothing.  This is just to wake us up. */
}

static void
sigterm_handler (int dummy)
{
	shutdown = 2;
}

int
main (int argc, char **argv, char **envp)
{
	InferiorHandle *handle = NULL;
	GIOChannel *status_channel;
	struct stat statb;
	const int command_fd = 4, status_fd = 3;
	int version, pid, attached;
	sigset_t mask, oldmask;
	GSpawnFlags flags;
	GError *error = NULL;

	if (argc < 4)
		usage ();

	if (strcmp (argv [1], MONO_SYMBOL_FILE_MAGIC))
		usage ();
	if (sscanf (argv [2], "%d", &version) != 1)
		usage ();

	if (version != MONO_SYMBOL_FILE_VERSION)
		g_error (G_STRLOC ": Incorrect server version; this is %d, but our caller expects %d.",
			 MONO_SYMBOL_FILE_VERSION, version);

	g_thread_init (NULL);

	if (fstat (command_fd, &statb)) {
		g_warning (G_STRLOC ": Can't fstat (%d): %s", command_fd, g_strerror (errno));
		usage ();
	}

	if (fstat (status_fd, &statb)) {
		g_warning (G_STRLOC ": Can't fstat (%d): %s", status_fd, g_strerror (errno));
		usage ();
	}

	status_channel = g_io_channel_unix_new (status_fd);
	if (g_io_channel_set_encoding (status_channel, NULL, &error) != G_IO_STATUS_NORMAL)
		g_error (G_STRLOC ": Can't set encoding on status channel: %s", error->message);
	g_io_channel_set_buffered (status_channel, FALSE);

	flags = G_SPAWN_SEARCH_PATH | G_SPAWN_DO_NOT_REAP_CHILD;

	if (!strcmp (argv [3], "0")) {
		if (argc < 6)
			usage ();

		if (!g_spawn_async (argv [4], argv + 5, envp, flags, child_setup_func, NULL, &pid, &error))
			g_error (G_STRLOC ": Can't spawn child: %s", error->message);

		handle = server_ptrace_get_handle (pid);
		attached = FALSE;
	} else {
		if (sscanf (argv [3], "%d", &pid) != 1)
			usage ();

		handle = server_ptrace_attach (pid);
		attached = TRUE;
	}

	/* Set up the mask of signals to temporarily block. */
	sigemptyset (&mask);
	sigaddset (&mask, SIGUSR1);
	sigaddset (&mask, SIGCHLD);
	sigaddset (&mask, SIGTERM);

	signal (SIGUSR1, command_signal_handler);
	signal (SIGCHLD, signal_handler);
	signal (SIGTERM, sigterm_handler);

	while (!shutdown) {
		int ret, status;

		ret = waitpid (0, &status, WNOHANG | WUNTRACED);
		if (ret < 0)
			g_error (G_STRLOC ": Can't waitpid (%d): %s", pid, g_strerror (errno));

		if (ret != 0) {
			if (WIFSTOPPED (status)) {
				send_status_message (status_channel, MESSAGE_CHILD_STOPPED,
						     WSTOPSIG (status));
			} else if (WIFEXITED (status)) {
				send_status_message (status_channel, MESSAGE_CHILD_EXITED,
						     WEXITSTATUS (status));
				return 0;
			} else if (WIFSIGNALED (status)) {
				send_status_message (status_channel, MESSAGE_CHILD_SIGNALED,
						     WTERMSIG (status));
				return 0;
			} else
				g_message (G_STRLOC ": %d - %d", ret, status);

			continue;
		}

		/* Wait for a signal to arrive. */
		sigprocmask (SIG_BLOCK, &mask, &oldmask);
		sigsuspend (&oldmask);
		sigprocmask (SIG_UNBLOCK, &mask, NULL);

		if (command_available) {
			command_available = FALSE;
			command_func (handle, command_fd);
		}
	}

	/* If we attached to a running process, detach from it, otherwise kill it. */
	if (attached)
		server_ptrace_detach (handle);
	else if (shutdown == 2)
		kill (pid, SIGKILL);
	else
		kill (pid, SIGTERM);

	return 0;
}
