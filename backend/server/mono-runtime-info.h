#ifndef __MONO_DEBUGGER_MONO_RUNTIME_INFO_H__
#define __MONO_DEBUGGER_MONO_RUNTIME_INFO_H__

#include <glib.h>

G_BEGIN_DECLS

typedef struct _MonoRuntimeInfoPriv MonoRuntimeInfoPriv;

typedef struct
{
	guint32 address_size;
	guint64 notification_address;
	guint64 executable_code_buffer;
	guint32 executable_code_buffer_size;
	guint64 breakpoint_info_area;
	guint64 breakpoint_table;
	guint32 breakpoint_table_size;
	MonoRuntimeInfoPriv *_priv;
} MonoRuntimeInfo;

MonoRuntimeInfo *
mono_debugger_server_initialize_mono_runtime (guint32 address_size,
					      guint64 notification_address,
					      guint64 executable_code_buffer,
					      guint32 executable_code_buffer_size,
					      guint64 breakpoint_info_area,
					      guint64 breakpoint_table,
					      guint32 breakpoint_table_size);

G_END_DECLS

#endif