using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Debugger.Languages;
using Mono.Debugger.Languages.Mono;

namespace Mono.Debugger.Backends
{
	internal class CoreFile : ProcessServant
	{
		TargetInfo info;
		Bfd bfd, core_bfd;
		string core_file;
		string application;
		ArrayList threads;

		MonoDebuggerInfo debugger_info;

		public static CoreFile OpenCoreFile (ThreadManager manager, ProcessStart start)
		{
			return new CoreFile (manager, start);
		}

		protected CoreFile (ThreadManager manager, ProcessStart start)
			: base (manager, start)
		{
			info = Inferior.GetTargetInfo (manager.AddressDomain);

			bfd = BfdContainer.AddFile (
				info, start.TargetApplication, TargetAddress.Null,
				start.LoadNativeSymbolTable, true);

			BfdContainer.SetupInferior (info, bfd);

			core_file = start.CoreFile;
			application = bfd.FileName;

			core_bfd = bfd.OpenCoreFile (core_file);

#if FIXME
			string crash_program = core_bfd.CrashProgram;
			string[] crash_program_args = crash_program.Split (' ');

			if (crash_program_args [0] != application)
				throw new TargetException (
					TargetError.CannotStartTarget,
					"Core file (generated from {0}) doesn't match executable {1}.",
					crash_program, application);

			bool ok;
			try {
				DateTime core_date = Directory.GetLastWriteTime (core_file);
				DateTime app_date = Directory.GetLastWriteTime (application);

				ok = app_date < core_date;
			} catch {
				ok = false;
			}

			if (!ok)
				throw new TargetException (
					TargetError.CannotStartTarget,
					"Executable {0} is more recent than core file {1}.",
					application, core_file);
#endif

			read_note_section ();
			main_thread = (CoreFileThread) threads [0];

			bfd.UpdateSharedLibraryInfo (null, main_thread);

			TargetAddress mdb_debug_info = bfd.GetSectionAddress (".mdb_debug_info");
			if (!mdb_debug_info.IsNull) {
				mdb_debug_info = main_thread.ReadAddress (mdb_debug_info);
				debugger_info = MonoDebuggerInfo.Create (main_thread, mdb_debug_info);
				read_thread_table ();
				CreateMonoLanguage (debugger_info);
				mono_language.InitializeCoreFile (main_thread);
				mono_language.Update (main_thread);
			}
		}

		void read_thread_table ()
		{
			TargetAddress ptr = main_thread.ReadAddress (debugger_info.ThreadTable);
			Console.WriteLine ("READ THREAD TABLE: {0}", ptr);
			while (!ptr.IsNull) {
				int size = 56 + main_thread.TargetInfo.TargetAddressSize;
				TargetReader reader = new TargetReader (main_thread.ReadMemory (ptr, size));

				long tid = reader.ReadLongInteger ();
				TargetAddress lmf_addr = reader.ReadAddress ();
				TargetAddress end_stack = reader.ReadAddress ();

				ptr = reader.ReadAddress ();

				TargetAddress stack_start = reader.ReadAddress ();
				TargetAddress signal_stack_start = reader.ReadAddress ();
				int stack_size = reader.ReadInteger ();
				int signal_stack_size = reader.ReadInteger ();

				Console.WriteLine ("READ THREAD TABLE #1: {0:x} {1} {2} {3} {4} {5} {6}",
						   tid, lmf_addr, end_stack,
						   stack_start, stack_start + stack_size,
						   signal_stack_start,
						   signal_stack_start + signal_stack_size);

				bool found = false;
				foreach (CoreFileThread thread in threads) {
					TargetAddress sp = thread.CurrentFrame.StackPointer;

					if ((sp >= stack_start) && (sp < stack_start + stack_size)) {
						Console.WriteLine ("SET LMF: {0} {1:x} {2}",
								   thread, tid, lmf_addr);
						thread.SetLMFAddress (tid, lmf_addr);
						found = true;
						break;
					} else if (!signal_stack_start.IsNull &&
						   (sp >= signal_stack_start) &&
						   (sp < signal_stack_start + signal_stack_size)) {
						Console.WriteLine ("SET LMF: {0} {1:x} {2}",
								   thread, tid, lmf_addr);
						thread.SetLMFAddress (tid, lmf_addr);
						found = true;
						break;
					}
				}

				if (!found)
					Console.WriteLine ("FUCK!");
			}
		}

		protected TargetReader GetReader (TargetAddress address)
		{
			TargetReader reader = core_bfd.GetReader (address, true);
			if (reader != null)
				return reader;

			Bfd bfd = BfdContainer.LookupLibrary (address);
			if (bfd != null)
				return bfd.GetReader (address, false);

			return null;
		}

		void read_note_section ()
		{
			threads = new ArrayList ();
			foreach (Bfd.Section section in core_bfd.Sections) {
				if (!section.name.StartsWith (".reg/"))
					continue;

				int pid = Int32.Parse (section.name.Substring (5));
				CoreFileThread thread = new CoreFileThread (this, pid);
				OnThreadCreatedEvent (thread);
				threads.Add (thread);
			}

#if FIXME
			TargetReader reader = core_bfd.GetSectionReader ("note0");
			while (reader.Offset < reader.Size) {
				long offset = reader.Offset;
				int namesz = reader.ReadInteger ();
				int descsz = reader.ReadInteger ();
				int type = reader.ReadInteger ();

				string name = null;
				if (namesz != 0) {
					char[] namebuf = new char [namesz];
					for (int i = 0; i < namesz; i++)
						namebuf [i] = (char) reader.ReadByte ();

					name = new String (namebuf);
				}

				byte[] desc = null;
				if (descsz != 0)
					desc = reader.BinaryReader.ReadBuffer (descsz);

				// Console.WriteLine ("NOTE: {0} {1:x} {2}", offset, type, name);
				// Console.WriteLine (TargetBinaryReader.HexDump (desc));

				reader.Offset += 4 - (reader.Offset % 4);
			}
#endif
		}

		public Bfd Bfd {
			get { return bfd; }
		}

		public Bfd CoreBfd {
			get { return core_bfd; }
		}

		public Architecture Architecture {
			get { return bfd.Architecture; }
		}

		public TargetInfo TargetInfo {
			get { return info; }
		}

		protected class CoreFileThread : ThreadServant
		{
			public readonly CoreFile CoreFile;
			public readonly Thread Thread;
			public readonly Registers Registers;
			public readonly BfdDisassembler Disassembler;
			Backtrace current_backtrace;
			StackFrame current_frame;
			Method current_method;
			long tid;
			int pid;

			TargetAddress lmf_address = TargetAddress.Null;

			public CoreFileThread (CoreFile core, int pid)
				: base (core.ThreadManager, core)
			{
				this.pid = pid;
				this.CoreFile = core;
				this.Thread = new Thread (this, ID);

				this.Disassembler = core.CoreBfd.GetDisassembler (this);
				this.Registers = read_registers ();
			}

			Registers read_registers ()
			{
				string sname = String.Format (".reg/{0}", PID);
				TargetReader reader = CoreFile.CoreBfd.GetSectionReader (sname);

				Architecture arch = CoreFile.Architecture;
				long[] values = new long [arch.CountRegisters];
				for (int i = 0; i < values.Length; i++) {
					int size = arch.RegisterSizes [i];
					if (size == 4)
						values [i] = reader.BinaryReader.ReadInt32 ();
					else if (size == 8)
						values [i] = reader.BinaryReader.ReadInt64 ();
					else
						throw new InternalError ();
				}

				return new Registers (arch, values);
			}

			internal void SetLMFAddress (long tid, TargetAddress lmf)
			{
				this.tid = tid;
				this.lmf_address = lmf;
			}

			internal override ThreadManager ThreadManager {
				get { return CoreFile.ThreadManager; }
			}

			internal override ProcessServant ProcessServant {
				get { return CoreFile; }
			}

			public override TargetEventArgs LastTargetEvent {
				get { throw new InvalidOperationException (); }
			}

			public override TargetInfo TargetInfo {
				get { return CoreFile.TargetInfo; }
			}

			public override int PID {
				get { return pid; }
			}

			public override long TID {
				get { return tid; }
			}

			public override TargetAddress LMFAddress {
				get { return lmf_address; }
			}

			public override bool IsAlive {
				get { return true; }
			}

			public override bool CanRun {
				get { return false; }
			}

			public override bool CanStep {
				get { return false; }
			}

			public override bool IsStopped {
				get { return true; }
			}

			public override Backtrace GetBacktrace (Backtrace.Mode mode, int max_frames)
			{
				current_backtrace = new Backtrace (CurrentFrame);

				current_backtrace.GetBacktrace (
					this, mode, TargetAddress.Null, max_frames);

				return current_backtrace;
			}

			public override TargetState State {
				get { return TargetState.CoreFile; }
			}

			public override StackFrame CurrentFrame {
				get {
					if (current_frame == null)
						current_frame = CoreFile.Architecture.CreateFrame (
							Thread, Registers, true);

					return current_frame;
				}
			}

			public override Method CurrentMethod {
				get {
					if (current_method == null)
						current_method = Lookup (CurrentFrameAddress);

					return current_method;
				}
			}

			public override TargetAddress CurrentFrameAddress {
				get { return CurrentFrame.TargetAddress; }
			}

			public override Backtrace CurrentBacktrace {
				get { return current_backtrace; }
			}

			public override Registers GetRegisters ()
			{
				return Registers;
			}

			public override int GetInstructionSize (TargetAddress address)
			{
				return Disassembler.GetInstructionSize (address);
			}

			public override AssemblerLine DisassembleInstruction (Method method,
									      TargetAddress address)
			{
				return Disassembler.DisassembleInstruction (method, address);
			}

			public override AssemblerMethod DisassembleMethod (Method method)
			{
				return Disassembler.DisassembleMethod (method);
			}

			public override TargetMemoryArea[] GetMemoryMaps ()
			{
				throw new NotImplementedException ();
			}

			public override Method Lookup (TargetAddress address)
			{
				return CoreFile.SymbolTableManager.Lookup (address);
			}

			public override Symbol SimpleLookup (TargetAddress address, bool exact_match)
			{
				return CoreFile.SymbolTableManager.SimpleLookup (address, exact_match);
			}

			internal override object Invoke (TargetAccessDelegate func, object data)
			{
				throw new InvalidOperationException ();
			}

			internal override void AcquireThreadLock ()
			{
				throw new InvalidOperationException ();
			}

			internal override void ReleaseThreadLock ()
			{
				throw new InvalidOperationException ();
			}

			internal override void ReleaseThreadLockDone ()
			{
				throw new InvalidOperationException ();
			}

			//
			// TargetMemoryAccess
			//

			public override AddressDomain AddressDomain {
				get { return CoreFile.TargetInfo.AddressDomain; }
			}

			internal override Architecture Architecture {
				get { return CoreFile.Architecture; }
			}

			public override byte ReadByte (TargetAddress address)
			{
				return CoreFile.GetReader (address).ReadByte ();
			}

			public override int ReadInteger (TargetAddress address)
			{
				return CoreFile.GetReader (address).ReadInteger ();
			}

			public override long ReadLongInteger (TargetAddress address)
			{
				return CoreFile.GetReader (address).ReadLongInteger ();
			}

			public override TargetAddress ReadAddress (TargetAddress address)
			{
				return CoreFile.GetReader (address).ReadAddress ();
			}

			public override string ReadString (TargetAddress address)
			{
				return CoreFile.GetReader (address).BinaryReader.ReadString ();
			}

			public override TargetBlob ReadMemory (TargetAddress address, int size)
			{
				return new TargetBlob (ReadBuffer (address, size), TargetInfo);
			}

			public override byte[] ReadBuffer (TargetAddress address, int size)
			{
				return CoreFile.GetReader (address).BinaryReader.ReadBuffer (size);
			}

			internal override Registers GetCallbackFrame (TargetAddress stack_pointer,
								      bool exact_match)
			{
				return null;
			}

			public override bool CanWrite {
				get { return false; }
			}

			public override void WriteBuffer (TargetAddress address, byte[] buffer)
			{
				throw new InvalidOperationException ();
			}

			public override void WriteByte (TargetAddress address, byte value)
			{
				throw new InvalidOperationException ();
			}

			public override void WriteInteger (TargetAddress address, int value)
			{
				throw new InvalidOperationException ();
			}

			public override void WriteLongInteger (TargetAddress address, long value)
			{
				throw new InvalidOperationException ();
			}

			public override void WriteAddress (TargetAddress address, TargetAddress value)
			{
				throw new InvalidOperationException ();
			}

			public override void SetRegisters (Registers registers)
			{
				throw new InvalidOperationException ();
			}

			internal override void InsertBreakpoint (BreakpointHandle handle,
								 TargetAddress address, int domain)
			{
				throw new InvalidOperationException ();
			}

			internal override void RemoveBreakpoint (BreakpointHandle handle)
			{
				throw new InvalidOperationException ();
			}

			public override void StepInstruction (CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void StepNativeInstruction (CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void NextInstruction (CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void StepLine (CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void NextLine (CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void Finish (bool native, CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void Continue (TargetAddress until, CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void Background (TargetAddress until, CommandResult result)
			{
				throw new InvalidOperationException ();
			}

			public override void Kill ()
			{ }

			public override void Detach ()
			{
				throw new InvalidOperationException ();
			}

			internal override void DetachThread ()
			{
				throw new InvalidOperationException ();
			}

			public override void Stop ()
			{
				throw new InvalidOperationException ();
			}

			public override int AddEventHandler (Event handle)
			{
				throw new InvalidOperationException ();
			}

			public override void RemoveEventHandler (int index)
			{
				throw new InvalidOperationException ();
			}

			public override string PrintObject (Style style, TargetObject obj,
							    DisplayFormat format)
			{
				return style.FormatObject (Thread, obj, format);
			}

			public override string PrintType (Style style, TargetType type)
			{
				return style.FormatType (Thread, type);
			}

			public override void RuntimeInvoke (TargetFunctionType function,
							    TargetClassObject object_argument,
							    TargetObject[] param_objects,
							    bool is_virtual, bool debug,
							    RuntimeInvokeResult result)
			{
				throw new InvalidOperationException ();
			}

			public override CommandResult CallMethod (TargetAddress method,
								  long arg1, long arg2)
			{
				throw new InvalidOperationException ();
			}

			public override CommandResult CallMethod (TargetAddress method,
								  long arg1, long arg2,
								  string string_arg)
			{
				throw new InvalidOperationException ();
			}

			public override CommandResult CallMethod (TargetAddress method,
								  TargetAddress arg)
			{
				throw new InvalidOperationException ();
			}

			public override CommandResult Return (bool run_finally)
			{
				throw new InvalidOperationException ();
			}

			public override CommandResult AbortInvocation ()
			{
				throw new InvalidOperationException ();
			}
		}

		//
		// IDisposable
		//

		protected override void DoDispose ()
		{
			if (core_bfd != null)
				core_bfd.Dispose ();
			base.DoDispose ();
		}
	}
}