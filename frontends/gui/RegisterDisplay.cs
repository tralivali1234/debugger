//
// The register display code.
//
// Authors:
//   Martin Baulig (martin@gnome.org)
//   Miguel de Icaza (miguel@ximian.com)
//
// The register display will have to be implemented once per target
// so that we can achieve the best display layout for the registers, as
// a general purpose routine will not serve the purposes we need.
//
// Currently the only supported view is the x86 view.
//
// The actual widgets are laid out in Glade, and we use a notebook with various
// pages, and depending on the architecture, we select the proper page to show.
//
// Strategy:
//    I also want to widget.Hide () most of the useless registers and flags
//    except when the "advanced" mode is turned on, and then everything is shown.
//
// (C) 2002 Ximian, Inc.
//
using GLib;
using Gtk;
using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using Mono.Debugger;

namespace Mono.Debugger.GUI
{
	public class RegisterDisplay : DebuggerWidget
	{
		Gtk.Notebook notebook;
		IArchitecture arch;
		Gdk.Color color_change, color_stable;
		bool active;

		public RegisterDisplay (DebuggerGUI gui, string glade_name)
			: this (gui, (Gtk.Notebook) gui.GXML [glade_name])
		{ }

		public RegisterDisplay (DebuggerGUI gui, Gtk.Notebook notebook)
			: base (gui, null, notebook)
		{
			this.notebook = notebook;

			manager.RealFrameChangedEvent += new StackFrameHandler (RealFrameChanged);
			manager.RealFramesInvalidEvent += new StackFrameInvalidHandler (RealFramesInvalid);

			color_change.red = 0xffff;
			color_change.green = 0;
			color_change.blue = 0;

			color_stable.red = 0;
			color_stable.green = 0;
			color_stable.blue = 0;
		}

		protected override void SetProcess (Process process)
		{
			base.SetProcess (process);

			arch = process.Architecture;
			if (arch != null)
				SetupI386 ();
		}

		protected override void StateChanged (TargetState new_state, int arg)
		{
			if (!active)
				return;

			switch (new_state) {
			case TargetState.STOPPED:
			case TargetState.CORE_FILE:
				if (regs != null)
					notebook.Page = 1;
				else
					notebook.Page = 0;
				UpdateDisplay ();
				break;

			default:
				// Do not change the page, it flickers.
				break;
			}
		}

		public bool Active {
			get {
				return active;
			}

			set {
				if (active == value)
					return;
				active = value;
				if (active && (regs != null)) {
					notebook.Page = 1;
					UpdateDisplay ();
				} else
					notebook.Page = 0;
			}
		}

		
		//
		// The i386 registers
		//
		Gtk.Entry [] i386_registers;
		Gtk.ToggleButton i386_cf, i386_pf, i386_af, i386_zf;
		Gtk.ToggleButton i386_sf, i386_tf, i386_if, i386_df;
		Gtk.ToggleButton i386_of, i386_nt, i386_rf, i386_vm;
		Gtk.ToggleButton i386_ac, i386_vif, i386_id, i386_vip;
		bool i386_initialized = false;

		void i386_register_modified (object sender, EventArgs e)
		{
			long value;
			int ridx;
			
			for (ridx = 0; ridx < (int) I386Register.COUNT; ridx++){
				if (sender == i386_registers [ridx])
					break;
			}
			
			if (process.State != TargetState.STOPPED){
				Report.Error ("Can not set value, program is not stopped");
				value = regs [ridx];
			} else {
				Gtk.Entry entry = (Gtk.Entry) sender;
				string text = entry.Text;

				try {
					value = UInt32.Parse (text, NumberStyles.HexNumber);
				} catch {
					Report.Error ("Invalid value entered");
					value = regs [ridx];
				}
			}

			i386_registers [ridx].Text = String.Format ("{0:x8}", value);
			process.SetRegister (ridx, value);
		}
		
		//
		// Loads the pointers to the i386 widgets, and hooks them up their
		// event handlers
		//
		void I386BindWidgets ()
		{
			if (i386_registers != null)
				return;

			i386_registers = new Gtk.Entry [(int) I386Register.COUNT];
			for (int i = 0; i <= (int) I386Register.COUNT; i++){
				if (i == (int) I386Register.EFLAGS)
					continue;
				
				string name = ((I386Register) i).ToString ();
				if (name.Length != 3)
					continue;

				string full = String.Format ("386-{0}-entry", name.ToLower ());

				Gtk.Entry entry = (Gtk.Entry) gxml [full];
				entry.Activated += new EventHandler (i386_register_modified);
				i386_registers [i] = entry;
			}
			
			i386_cf = (Gtk.ToggleButton) gxml ["386-carry-flag"];
			i386_pf = (Gtk.ToggleButton) gxml ["386-parity-flag"];
			i386_af = (Gtk.ToggleButton) gxml ["386-auxiliary-carry-flag"];
			i386_zf = (Gtk.ToggleButton) gxml ["386-zero-flag"];
			i386_sf = (Gtk.ToggleButton) gxml ["386-sign-flag"];
			i386_tf = (Gtk.ToggleButton) gxml ["386-trap-flag"];
			i386_if = (Gtk.ToggleButton) gxml ["386-interrupt-enable-flag"];
			i386_df = (Gtk.ToggleButton) gxml ["386-direction-flag"];
			i386_of = (Gtk.ToggleButton) gxml ["386-overflow-flag"];
			i386_nt = (Gtk.ToggleButton) gxml ["386-nested-task-flag"];
			i386_rf = (Gtk.ToggleButton) gxml ["386-resume-flag"];
			i386_vm = (Gtk.ToggleButton) gxml ["386-vm-flag"];
			i386_ac = (Gtk.ToggleButton) gxml ["386-align-check-flag"];
			i386_vif = (Gtk.ToggleButton) gxml ["386-vi-flag"];
			i386_id = (Gtk.ToggleButton) gxml ["386-id-flag"];
			i386_vip = (Gtk.ToggleButton) gxml ["386-vip-flag"];

			I386_SetAdvanced (false);
		}

		void I386_SetAdvanced (bool state)
		{
			foreach (string s in new string [] { "cs", "ds", "es", "ss", "fs", "gs"}){
				string label = String.Format ("x{0}-label", s);
				gxml [label].Visible = false;
				string entry = String.Format ("386-x{0}-entry", s);
				gxml [entry].Visible = false;
			}
		}
		
		//
		// Sets the widgets to editable/non-editable depending on whether the backend
		// supports editing or not
		//
		void I386SetupModifiableWidgets ()
		{
			bool can_modify = process.State != TargetState.CORE_FILE;

			for (int i = 0; i < (int) I386Register.COUNT; i++){
				if (i386_registers [i] != null)
					i386_registers [i].Editable = can_modify;
			}
		}

		//
		// Sets 
		void SetupI386 ()
		{
			I386BindWidgets ();
			I386SetupModifiableWidgets ();

			if (i386_initialized)
				return;

			i386_initialized = true;
		}
		
		long [] last_regs, regs;

		void SetText (Gtk.Entry entry, int idx)
		{
			if (last_regs != null){
				if (regs [idx] != last_regs [idx]){
					entry.Text = String.Format ("{0:X8}", regs [idx]);
					entry.ModifyText (Gtk.StateType.Normal, color_change);
				} else {
					entry.ModifyText (Gtk.StateType.Normal, color_stable);
				}
			} else {
				entry.Text = String.Format ("{0:X8}", regs [idx]);
				entry.ModifyText (Gtk.StateType.Normal, color_stable);
			}
		}

		public void UpdateDisplay ()
		{
			if (!IsVisible)
				return;
			
			if ((CurrentFrame == null) || (arch == null))
				return;

			if (regs == null) {
				last_regs = regs;
				return;
			}
			
			for (int i = 0; i < (int) I386Register.COUNT; i++){
				if (i386_registers [i] == null)
					continue;
				SetText (i386_registers [i], i);
			}

			long f = regs [(int)I386Register.EFLAGS];
			i386_cf.Active =  ((f & (1 << 0)) != 0);
			i386_pf.Active =  ((f & (1 << 2)) != 0);
			i386_af.Active =  ((f & (1 << 4)) != 0);
			i386_zf.Active =  ((f & (1 << 6)) != 0);
			i386_sf.Active =  ((f & (1 << 7)) != 0);
			i386_tf.Active =  ((f & (1 << 8)) != 0);
			i386_if.Active =  ((f & (1 << 9)) != 0);
			i386_df.Active =  ((f & (1 << 10)) != 0);
			i386_of.Active =  ((f & (1 << 11)) != 0);
			i386_nt.Active =  ((f & (1 << 14)) != 0);
			i386_rf.Active =  ((f & (1 << 16)) != 0);
			i386_vm.Active =  ((f & (1 << 17)) != 0);
			i386_ac.Active =  ((f & (1 << 18)) != 0);
			i386_vif.Active = ((f & (1 << 19)) != 0);
			i386_id.Active =  ((f & (1 << 21)) != 0);
			i386_vip.Active = ((f & (1 << 20)) != 0);

			last_regs = regs;
		}
		
		void RealFrameChanged (StackFrame frame)
		{
			if (!process.HasTarget || (arch == null))
				return;

			try {
				regs = process.GetRegisters (arch.AllRegisterIndices);
			} catch (Exception e) {
				Console.WriteLine ("ERROR: Register loading threw an exception here: {0}", e);
				regs = null;
			}
		}

		void RealFramesInvalid ()
		{
			regs = null;
		}
	}
}
