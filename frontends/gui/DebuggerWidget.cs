using Gtk;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Mono.Debugger.GUI
{
	public abstract class DebuggerWidget
	{
		protected Gtk.Widget container;
		protected Gtk.Widget widget;
		protected DebuggerBackend backend;
		protected Process process;
		protected DebuggerGUI gui;
		protected Glade.XML gxml;
		bool visible;

		[DllImport("glib-2.0")]
		static extern bool g_main_context_iteration (IntPtr context, bool may_block);

		public DebuggerWidget (DebuggerGUI gui, Gtk.Container container, Gtk.Widget widget)
		{
			this.gui = gui;
			this.gxml = gui.GXML;
			this.widget = widget;
			this.container = container;

			if (container == null) {
				Gtk.Widget parent = widget.Parent;
				while (!(parent is Gtk.Window))
					parent = parent.Parent;

				container = (Gtk.Container) parent;
			}

			visible = container.Visible;

			try {
				container.Mapped += new EventHandler (mapped);
				container.Unmapped += new EventHandler (unmapped);
			} catch {}
		}

		public virtual void SetBackend (DebuggerBackend backend, Process process)
		{
			this.backend = backend;
			this.process = process;
		}
		
		void mapped (object o, EventArgs args)
		{
			visible = true;
		}

		void unmapped (object o, EventArgs args)
		{
			visible = false;
		}

		public DebuggerWidget (DebuggerGUI gui, Gtk.Widget widget)
			: this (gui, null, widget)
		{ }

		public virtual Gtk.Widget Widget {
			get {
				return widget;
			}
		}

		public virtual Gtk.Widget Container {
			get {
				return container;
			}
		}

		public virtual DebuggerBackend Debugger {
			get {
				return backend;
			}
		}

		public virtual void Show ()
		{
			container.Show ();
			visible = true;
		}

		public virtual void Hide ()
		{
			container.Hide ();
			visible = false;
		}

		public virtual bool IsVisible {
			get {
				return visible;
			}
		}

                protected void MainIteration ()
		{
			while (g_main_context_iteration (IntPtr.Zero, false))
				;
		}
	}
}
