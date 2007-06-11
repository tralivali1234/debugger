#include <config.h>
#include <stdlib.h>
#include <string.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/metadata.h>
#include <mono/metadata/tabledefs.h>
#include <mono/metadata/tokentype.h>
#include <mono/metadata/appdomain.h>
#include <mono/metadata/gc-internal.h>
#include <mono/metadata/threads.h>
#include <mono/os/gc_wrapper.h>
#include <mono/metadata/object-internals.h>
#include <mono/metadata/class-internals.h>
#include <mono/metadata/exception.h>
#include <mono/metadata/mono-debug.h>
#include <mono/metadata/mono-debug-debugger.h>
#include <mono/metadata/mono-endian.h>

static guint32 debugger_lock_level = 0;
static CRITICAL_SECTION debugger_lock_mutex;
static gboolean must_reload_symtabs = FALSE;
static gboolean mono_debugger_use_debugger = FALSE;
static MonoObject *last_exception = NULL;

void (*mono_debugger_event_handler) (MonoDebuggerEvent event, guint64 data, guint64 arg) = NULL;

#define WRITE_UINT32(ptr,value) G_STMT_START {	\
	* ((guint32 *) ptr) = value;		\
	ptr += 4;				\
} G_STMT_END

#define WRITE_POINTER(ptr,value) G_STMT_START {	\
	* ((gpointer *) ptr) = (gpointer) (value); \
	ptr += sizeof (gpointer);		\
} G_STMT_END

#define WRITE_STRING(ptr,value) G_STMT_START {	\
	memcpy (ptr, value, strlen (value)+1);	\
	ptr += strlen (value)+1;		\
} G_STMT_END

typedef struct {
	gpointer stack_pointer;
	MonoObject *exception_obj;
	guint32 stop;
} MonoDebuggerExceptionInfo;

static int initialized = 0;

void
mono_debugger_lock (void)
{
	g_assert (initialized);
	EnterCriticalSection (&debugger_lock_mutex);
	debugger_lock_level++;
}

void
mono_debugger_unlock (void)
{
	g_assert (initialized);
	if (debugger_lock_level == 1) {
		if (must_reload_symtabs && mono_debugger_use_debugger) {
			mono_debugger_event (MONO_DEBUGGER_EVENT_RELOAD_SYMTABS, 0, 0);
			must_reload_symtabs = FALSE;
		}
	}

	debugger_lock_level--;
	LeaveCriticalSection (&debugger_lock_mutex);
}

void
mono_debugger_initialize (gboolean use_debugger)
{
	MONO_GC_REGISTER_ROOT (last_exception);
	
	g_assert (!mono_debugger_use_debugger);

	InitializeCriticalSection (&debugger_lock_mutex);
	mono_debugger_use_debugger = use_debugger;
	initialized = 1;
}

void
mono_debugger_add_symbol_file (MonoDebugHandle *handle)
{
	g_assert (mono_debugger_use_debugger);

	mono_debugger_lock ();
	mono_debugger_event (MONO_DEBUGGER_EVENT_ADD_MODULE, (guint64) (gsize) handle, 0);
	mono_debugger_unlock ();
}

void
mono_debugger_event (MonoDebuggerEvent event, guint64 data, guint64 arg)
{
	if (mono_debugger_event_handler)
		(* mono_debugger_event_handler) (event, data, arg);
}

void
mono_debugger_cleanup (void)
{
	mono_debugger_event (MONO_DEBUGGER_EVENT_FINALIZE_MANAGED_CODE, 0, 0);
	mono_debugger_event_handler = NULL;
}

/*
 * Debugger breakpoint interface.
 *
 * This interface is used to insert breakpoints on methods which are not yet JITed.
 * The debugging code keeps a list of all such breakpoints and automatically inserts the
 * breakpoint when the method is JITed.
 */

static GPtrArray *breakpoints = NULL;

int
mono_debugger_insert_breakpoint_full (MonoMethodDesc *desc)
{
	static int last_breakpoint_id = 0;
	MonoDebuggerBreakpointInfo *info;

	info = g_new0 (MonoDebuggerBreakpointInfo, 1);
	info->desc = desc;
	info->index = ++last_breakpoint_id;

	if (!breakpoints)
		breakpoints = g_ptr_array_new ();

	g_ptr_array_add (breakpoints, info);

	return info->index;
}

int
mono_debugger_remove_breakpoint (int breakpoint_id)
{
	int i;

	if (!breakpoints)
		return 0;

	for (i = 0; i < breakpoints->len; i++) {
		MonoDebuggerBreakpointInfo *info = g_ptr_array_index (breakpoints, i);

		if (info->index != breakpoint_id)
			continue;

		mono_method_desc_free (info->desc);
		g_ptr_array_remove (breakpoints, info);
		g_free (info);
		return 1;
	}

	return 0;
}

int
mono_debugger_insert_breakpoint (const gchar *method_name, gboolean include_namespace)
{
	MonoMethodDesc *desc;

	desc = mono_method_desc_new (method_name, include_namespace);
	if (!desc)
		return 0;

	return mono_debugger_insert_breakpoint_full (desc);
}

int
mono_debugger_method_has_breakpoint (MonoMethod *method)
{
	int i;

	if (!breakpoints || (method->wrapper_type != MONO_WRAPPER_NONE))
		return 0;

	for (i = 0; i < breakpoints->len; i++) {
		MonoDebuggerBreakpointInfo *info = g_ptr_array_index (breakpoints, i);

		if (!mono_method_desc_full_match (info->desc, method))
			continue;

		return info->index;
	}

	return 0;
}

void
mono_debugger_breakpoint_callback (MonoMethod *method, guint32 index)
{
	mono_debugger_event (MONO_DEBUGGER_EVENT_JIT_BREAKPOINT, (guint64) (gsize) method, index);
}

gboolean
mono_debugger_unhandled_exception (gpointer addr, gpointer stack, MonoObject *exc)
{
	const gchar *name;

	if (!mono_debugger_use_debugger)
		return FALSE;

	// Prevent the object from being finalized.
	last_exception = exc;

	name = mono_class_get_name (mono_object_get_class (exc));
	if (!strcmp (name, "ThreadAbortException")) {
		MonoThread *thread = mono_thread_current ();
		mono_debugger_event (MONO_DEBUGGER_EVENT_THREAD_ABORT, 0, thread->tid);
		mono_thread_exit ();
	}

	mono_debugger_event (MONO_DEBUGGER_EVENT_UNHANDLED_EXCEPTION,
			     (guint64) (gsize) exc, (guint64) (gsize) addr);
	return TRUE;
}

void
mono_debugger_handle_exception (gpointer addr, gpointer stack, MonoObject *exc)
{
	MonoDebuggerExceptionInfo info;

	if (!mono_debugger_use_debugger)
		return;

	// Prevent the object from being finalized.
	last_exception = exc;

	info.stack_pointer = stack;
	info.exception_obj = exc;
	info.stop = 0;

	mono_debugger_event (MONO_DEBUGGER_EVENT_HANDLE_EXCEPTION, (guint64) (gsize) &info,
			     (guint64) (gsize) addr);
}

gboolean
mono_debugger_throw_exception (gpointer addr, gpointer stack, MonoObject *exc)
{
	MonoDebuggerExceptionInfo info;

	if (!mono_debugger_use_debugger)
		return FALSE;

	// Prevent the object from being finalized.
	last_exception = exc;

	info.stack_pointer = stack;
	info.exception_obj = exc;
	info.stop = 0;

	mono_debugger_event (MONO_DEBUGGER_EVENT_THROW_EXCEPTION, (guint64) (gsize) &info,
			     (guint64) (gsize) addr);
	return info.stop != 0;
}

static gchar *
get_exception_message (MonoObject *exc)
{
	char *message = NULL;
	MonoString *str; 
	MonoMethod *method;
	MonoClass *klass;
	gint i;

	if (mono_object_isinst (exc, mono_defaults.exception_class)) {
		klass = exc->vtable->klass;
		method = NULL;
		while (klass && method == NULL) {
			for (i = 0; i < klass->method.count; ++i) {
				method = klass->methods [i];
				if (!strcmp ("ToString", method->name) &&
				    mono_method_signature (method)->param_count == 0 &&
				    method->flags & METHOD_ATTRIBUTE_VIRTUAL &&
				    method->flags & METHOD_ATTRIBUTE_PUBLIC) {
					break;
				}
				method = NULL;
			}
			
			if (method == NULL)
				klass = klass->parent;
		}

		g_assert (method);

		str = (MonoString *) mono_runtime_invoke (method, exc, NULL, NULL);
		if (str)
			message = mono_string_to_utf8 (str);
	}

	return message;
}

MonoObject *
mono_debugger_runtime_invoke (MonoMethod *method, void *obj, void **params, MonoObject **exc)
{
	MonoObject *retval;
	gchar *message;

	if (!strcmp (method->name, ".ctor")) {
		retval = obj = mono_object_new (mono_domain_get (), method->klass);

		mono_runtime_invoke (method, obj, params, exc);
	} else
		retval = mono_runtime_invoke (method, obj, params, exc);

	if (!exc || (*exc == NULL))
		return retval;

	message = get_exception_message (*exc);
	if (message) {
		*exc = (MonoObject *) mono_string_new_wrapper (message);
		g_free (message);
	}

	return retval;
}

gboolean
mono_debugger_lookup_type (const gchar *type_name)
{
	int i;
	mono_debugger_lock ();

	for (i = 0; i < mono_symbol_table->num_symbol_files; i++) {
		MonoDebugHandle *symfile = mono_symbol_table->symbol_files [i];
		MonoType *type;
		MonoClass* klass;
		gchar *name;

		name = g_strdup (type_name);
		type = mono_reflection_type_from_name (name, symfile->image);
		g_free (name);
		if (!type)
			continue;

		klass = mono_class_from_mono_type (type);
		if (klass)
			mono_class_init (klass);

		mono_debugger_unlock ();
		return TRUE;
	}

	mono_debugger_unlock ();
	return FALSE;
}

gint32
mono_debugger_lookup_assembly (const gchar *name)
{
	MonoAssembly *assembly;
	MonoImageOpenStatus status;
	int i;

	mono_debugger_lock ();

 again:
	for (i = 0; i < mono_symbol_table->num_symbol_files; i++) {
		MonoDebugHandle *symfile = mono_symbol_table->symbol_files [i];

		if (!strcmp (symfile->image_file, name)) {
			mono_debugger_unlock ();
			return i;
		}
	}

	assembly = mono_assembly_open (name, &status);

	if (status != MONO_IMAGE_OK) {
		g_warning (G_STRLOC ": Cannot open image `%s'", name);
		mono_debugger_unlock ();
		return -1;
	}

	must_reload_symtabs = TRUE;
	goto again;
}

static GPtrArray *class_init_callbacks = NULL;
static GPtrArray *method_load_callbacks = NULL;

typedef struct {
	guint64 index;
	gchar *name_space;
	gchar *name;
} ClassInitCallback;

MonoClass *
mono_debugger_register_class_init_callback (MonoImage *image, guint64 index,
					    const gchar *full_name)
{
	ClassInitCallback *info;
	MonoClass *klass;
	gchar *name_space, *name, *pos;

	name = g_strdup (full_name);

	pos = strrchr (name, '.');
	if (pos) {
		name_space = name;
		*pos = 0;
		name = pos + 1;
	} else {
		name_space = NULL;
	}

	mono_loader_lock ();

	klass = mono_class_from_name (image, name_space ? name_space : "", name);
	g_message (G_STRLOC ": %p - %s - %p", image, full_name, klass);
	if (klass && klass->inited) {
		mono_loader_unlock ();
		return klass;
	}

	info = g_new0 (ClassInitCallback, 1);
	info->index = index;
	info->name_space = name_space;
	info->name = name;

	if (!class_init_callbacks)
		class_init_callbacks = g_ptr_array_new ();

	g_ptr_array_add (class_init_callbacks, info);
	mono_loader_unlock ();
	return NULL;
}

void
mono_debugger_remove_class_init_callback (int index)
{
	int i;

	if (!class_init_callbacks)
		return;

	for (i = 0; i < class_init_callbacks->len; i++) {
		ClassInitCallback *info = g_ptr_array_index (class_init_callbacks, i);

		if (info->index != index)
			continue;

		g_ptr_array_remove (class_init_callbacks, info);
		if (info->name_space)
			g_free (info->name_space);
		else
			g_free (info->name);
		g_free (info);
	}
}

void
mono_debugger_add_type (MonoDebugHandle *symfile, MonoClass *klass)
{
	int i;

	must_reload_symtabs = TRUE;

	if (!class_init_callbacks)
		return;

	for (i = 0; i < class_init_callbacks->len; i++) {
		ClassInitCallback *info = g_ptr_array_index (class_init_callbacks, i);

		if (info->name_space && strcmp (info->name_space, klass->name_space))
			continue;
		if (strcmp (info->name, klass->name))
			continue;

		mono_debugger_event (MONO_DEBUGGER_EVENT_CLASS_INITIALIZED,
				     (guint64) (gsize) klass, info->index);

		g_ptr_array_remove (class_init_callbacks, info);
		if (info->name_space)
			g_free (info->name_space);
		else
			g_free (info->name);
		g_free (info);
		return;
	}
}

typedef struct {
	guint64 index;
	MonoMethod *method;
} MethodLoadCallback;

void
mono_debugger_register_method_load_callback (guint64 index, MonoMethod *method)
{
	MethodLoadCallback *info;

	info = g_new0 (MethodLoadCallback, 1);
	info->index = index;
	info->method = method;

	if (!method_load_callbacks)
		method_load_callbacks = g_ptr_array_new ();

	g_ptr_array_add (method_load_callbacks, info);
}

void
mono_debugger_remove_method_load_callback (int index)
{
	int i;

	if (!method_load_callbacks)
		return;

	for (i = 0; i < method_load_callbacks->len; i++) {
		MethodLoadCallback *info = g_ptr_array_index (method_load_callbacks, i);

		if (info->index != index)
			continue;

		g_ptr_array_remove (method_load_callbacks, info);
		g_free (info);
	}
}