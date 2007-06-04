using System;
using System.IO;
using System.Collections;
using ST = System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Mono.Debugger.Backends;
using Mono.Debugger.Languages;
using Mono.Debugger.Languages.Mono;

namespace Mono.Debugger
{
	public class Process : DebuggerMarshalByRefObject
	{
		Debugger debugger;
		ProcessServant servant;

		int id = ++next_id;
		static int next_id = 0;

		static readonly string DirectorySeparatorStr = Path.DirectorySeparatorChar.ToString ();

		internal Process (Debugger debugger, ProcessServant servant)
		{
			this.debugger = debugger;
			this.servant = servant;
		}

		public int ID {
			get { return id; }
		}

		public Debugger Debugger {
			get { return debugger; }
		}

		public DebuggerSession Session {
			get { return servant.Session; }
		}

		internal ProcessServant Servant {
			get { return servant; }
		}

		public Thread MainThread {
			get { return servant.MainThread.Client; }
		}

		public bool IsManaged {
			get { return servant.IsManaged; }
		}

		public string TargetApplication {
			get { return servant.TargetApplication; }
		}

		public string[] CommandLineArguments {
			get { return servant.CommandLineArguments; }
		}

		public Language NativeLanguage {
			get { return servant.NativeLanguage; }
		}

		public void Kill ()
		{
			servant.Kill ();
		}

		public void Detach ()
		{
			servant.Detach ();
		}

		public void LoadLibrary (Thread thread, string filename)
		{
			servant.LoadLibrary (thread, filename);
		}

		public Module[] Modules {
			get { return servant.Modules; }
		}

		public SourceFile FindFile (string filename)
		{
			Module[] modules = servant.Modules;

			foreach (Module module in modules) {
				SourceFile file = module.FindFile (filename);
				if (file != null)
					return file;
			}

			if (Path.IsPathRooted (filename))
				return null;

			filename = Path.GetFullPath (Path.Combine (
				servant.ProcessStart.Options.WorkingDirectory, filename));

			foreach (Module module in modules) {
				SourceFile file = module.FindFile (filename);
				if (file != null)
					return file;
			}

			return null;
		}

		public SourceLocation FindMethod (string name)
		{
			foreach (Module module in Modules) {
				MethodSource method = module.FindMethod (name);
				
				if (method != null)
					return new SourceLocation (method);
			}

			return null;
		}

		public Thread[] GetThreads ()
		{
			return servant.GetThreads ();
		}

		public override string ToString ()
		{
			return String.Format ("Process #{0}", ID);
		}

		//
		// IDisposable
		//

		private bool disposed = false;

		private void check_disposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("Process");
		}

		private void Dispose (bool disposing)
		{
			// Check to see if Dispose has already been called.
			lock (this) {
				if (disposed)
					return;

				disposed = true;
			}

			// If this is a call to Dispose, dispose all managed resources.
			if (disposing) {
				if (servant != null) {
					servant.Dispose ();
					servant = null;
				}
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			// Take yourself off the Finalization queue
			GC.SuppressFinalize (this);
		}

		~Process ()
		{
			Dispose (false);
		}
	}
}