using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Mono.Debugger
{
	public delegate void ModulesChangedHandler ();
	public delegate void BreakpointsChangedHandler ();

	public class ModuleManager
	{
		ArrayList modules = new ArrayList ();

		public Module CreateModule (string name)
		{
			Module module = new Module (name);

			modules.Add (module);

			module.SymbolsLoadedEvent += new ModuleEventHandler (module_changed);
			module.SymbolsUnLoadedEvent += new ModuleEventHandler (module_changed);
			module.BreakpointsChangedEvent += new ModuleEventHandler (breakpoints_changed);

			module_changed (module);

			return module;
		}

		public event ModulesChangedHandler ModulesChanged;
		public event BreakpointsChangedHandler BreakpointsChanged;

		public Module[] Modules {
			get {
				Module[] retval = new Module [modules.Count];
				modules.CopyTo (retval, 0);
				return retval;
			}
		}

		int locked = 0;
		bool needs_module_update = false;
		bool needs_breakpoint_update = false;

		public void Lock ()
		{
			locked++;
		}

		public void UnLock ()
		{
			if (--locked > 0)
				return;

			if (needs_module_update) {
				needs_module_update = false;
				OnModulesChanged ();
			}
			if (needs_breakpoint_update) {
				needs_breakpoint_update = false;
				OnBreakpointsChanged ();
			}
		}

		public void UnLoadAllModules ()
		{
			Lock ();
			foreach (Module module in modules)
				module.ModuleData = null;
			UnLock ();
		}

		void module_changed (Module module)
		{
			OnModulesChanged ();
		}

		void breakpoints_changed (Module module)
		{
			OnBreakpointsChanged ();
		}

		protected virtual void OnModulesChanged ()
		{
			if (locked > 0) {
				needs_module_update = true;
				return;
			}

			if (ModulesChanged != null)
				ModulesChanged ();
		}

		protected virtual void OnBreakpointsChanged ()
		{
			if (locked > 0) {
				needs_breakpoint_update = true;
				return;
			}

			if (BreakpointsChanged != null)
				BreakpointsChanged ();
		}
	}
}
