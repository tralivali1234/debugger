using GLib;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Configuration;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

using Mono.Debugger;
using Mono.Debugger.Languages;
using Mono.Debugger.Architecture;
using Mono.CSharp.Debugger;

namespace Mono.Debugger.Backends
{
	internal class TargetInfo : ITargetInfo
	{
		int target_int_size;
		int target_long_size;
		int target_address_size;

		internal TargetInfo (int target_int_size, int target_long_size,
				     int target_address_size)
		{
			this.target_int_size = target_int_size;
			this.target_long_size = target_long_size;
			this.target_address_size = target_address_size;
		}

		int ITargetInfo.TargetIntegerSize {
			get {
				return target_int_size;
			}
		}

		int ITargetInfo.TargetLongIntegerSize {
			get {
				return target_long_size;
			}
		}

		int ITargetInfo.TargetAddressSize {
			get {
				return target_address_size;
			}
		}
	}

	internal class TargetReader : ITargetMemoryReader
	{
		byte[] data;
		BinaryReader reader;
		int offset;
		ITargetInfo target_info;

		internal TargetReader (byte[] data, ITargetInfo target_info)
		{
			this.reader = new BinaryReader (new MemoryStream (data));
			this.target_info = target_info;
			this.data = data;
			this.offset = 0;
		}

		public long Offset {
			get {
				return reader.BaseStream.Position;
			}

			set {
				reader.BaseStream.Position = value;
			}
		}

		public BinaryReader BinaryReader {
			get {
				return reader;
			}
		}

		public int TargetIntegerSize {
			get {
				return target_info.TargetIntegerSize;
			}
		}

		public int TargetLongIntegerSize {
			get {
				return target_info.TargetLongIntegerSize;
			}
		}

		public int TargetAddressSize {
			get {
				return target_info.TargetAddressSize;
			}
		}

		public byte ReadByte ()
		{
			return reader.ReadByte ();
		}

		public int ReadInteger ()
		{
			return reader.ReadInt32 ();
		}

		public long ReadLongInteger ()
		{
			return reader.ReadInt64 ();
		}

		public ITargetLocation ReadAddress ()
		{
			if (TargetAddressSize == 4)
				return new TargetLocation (reader.ReadInt32 ());
			else if (TargetAddressSize == 8)
				return new TargetLocation (reader.ReadInt64 ());
			else
				throw new TargetMemoryException (
					"Unknown target address size " + TargetAddressSize);
		}
	}

	internal class TargetMemoryStream : Stream
	{
		ITargetLocation location;
		ITargetInfo target_info;
		ITargetMemoryAccess memory;

		internal TargetMemoryStream (ITargetMemoryAccess memory, ITargetLocation location,
					     ITargetInfo target_info)
		{
			this.memory = memory;
			this.location = (ITargetLocation) location.Clone ();
			this.target_info = target_info;
		}

		public override bool CanRead {
			get {
				return true;
			}
		}

		public override bool CanSeek {
			get {
				return true;
			}
		}

		public override bool CanWrite {
			get {
				return memory.CanWrite;
			}
		}

		public override long Length {
			get {
				throw new NotSupportedException ();
			}
		}

		public override long Position {
			get {
				return location.Offset;
			}

			set {
				location.Offset = (int) value;
			}
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
                        int ref_point;

                        switch (origin) {
			case SeekOrigin.Begin:
				ref_point = 0;
				break;
			case SeekOrigin.Current:
				ref_point = location.Offset;
				break;
			case SeekOrigin.End:
				throw new NotSupportedException ();
			default:
				throw new ArgumentException();
                        }

			// FIXME: The stream would actually allow being seeked before its start.
			//        However, I don't know how our callers would deal with a negative
			//        Position.
			if (ref_point + offset < 0)
                                throw new IOException ("Attempted to seek before start of stream");

                        location.Offset = (int) (ref_point + offset);

                        return location.Offset;
                }

		public override void Flush ()
		{
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			try {
				byte[] retval = memory.ReadBuffer (location, count);
				retval.CopyTo (buffer, offset);
			} catch (Exception e) {
				throw new IOException ("Cannot read target memory", e);
			}

			location.Offset += count;
			return count;
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			try {
				if (offset != 0) {
					byte[] temp = new byte [count];
					Array.Copy (buffer, offset, temp, 0, count);
					memory.WriteBuffer (location, temp, count);
				} else
					memory.WriteBuffer (location, buffer, count);
			} catch (Exception e) {
				throw new IOException ("Cannot read target memory", e);
			}

			location.Offset += count;
		}
	}

	internal class MonoDebuggerInfo
	{
		public readonly ITargetLocation trampoline_code;
		public readonly ITargetLocation symbol_file_generation;
		public readonly ITargetLocation symbol_file_table;
		public readonly ITargetLocation update_symbol_file_table;
		public readonly ITargetLocation compile_method;

		internal MonoDebuggerInfo (ITargetMemoryReader reader)
		{
			reader.Offset = reader.TargetLongIntegerSize +
				2 * reader.TargetIntegerSize;
			trampoline_code = reader.ReadAddress ();
			symbol_file_generation = reader.ReadAddress ();
			symbol_file_table = reader.ReadAddress ();
			update_symbol_file_table = reader.ReadAddress ();
			compile_method = reader.ReadAddress ();
		}

		public override string ToString ()
		{
			return String.Format ("MonoDebuggerInfo ({0:x}, {1:x}, {2:x}, {3:x}, {4:x})",
					      trampoline_code, symbol_file_generation, symbol_file_table,
					      update_symbol_file_table, compile_method);
		}
	}

	internal class StackFrame : IStackFrame
	{
		public readonly ISourceLocation SourceLocation = null;
		public readonly ITargetLocation TargetLocation = null;
		public readonly ISymbolHandle SymbolHandle = null;
		public readonly IInferior Inferior;
		public readonly string Instruction;

		public StackFrame (IInferior inferior, ITargetLocation location,
				   ISourceLocation source, ISymbolHandle handle)
			: this (inferior, location)
		{
			SourceLocation = source;
			SymbolHandle = handle;
		}

		public StackFrame (IInferior inferior, ITargetLocation location)
		{
			Inferior = inferior;
			TargetLocation = location;

			if (Inferior.Disassembler != null) {
				ITargetLocation loc = (ITargetLocation) location.Clone ();

				try {
					Instruction = Inferior.Disassembler.DisassembleInstruction (ref loc);
				} catch (TargetException e) {
					// Catch any target exceptions.
				}
			}
		}

		ISourceLocation IStackFrame.SourceLocation {
			get {
				return SourceLocation;
			}
		}

		ITargetLocation IStackFrame.TargetLocation {
			get {
				return TargetLocation;
			}
		}

		public override string ToString ()
		{
			StringBuilder builder = new StringBuilder ();

			if (SourceLocation != null) {
				builder.Append (SourceLocation);
				builder.Append (" at ");
			}
			builder.Append (TargetLocation);

			bool print_insn = true;
			if (print_insn && (Instruction != null)) {
				builder.Append (" (");
				builder.Append (Instruction);
				builder.Append (")");
			}

			return builder.ToString ();
		}
	}

	internal delegate void TargetAsyncCallback (object user_data, object result);

	internal class TargetAsyncResult
	{
		object user_data;
		bool completed;
		TargetAsyncCallback callback;

		internal TargetAsyncResult (TargetAsyncCallback callback, object user_data)
		{
			this.callback = callback;
			this.user_data = user_data;
		}

		public void Completed (object result)
		{
			if (completed)
				throw new InvalidOperationException ();

			completed = true;
			if (callback != null)
				callback (user_data, result);
		}

		public bool IsCompleted {
			get {
				return completed;
			}
		}
	}

	public class Debugger : IDebuggerBackend, IDisposable
	{
		public readonly string Path_Mono	= "mono";
		public readonly string Environment_Path	= "/usr/bin";

		ISourceFileFactory source_file_factory;

		Assembly application;
		Inferior inferior;

		readonly uint target_address_size;
		readonly uint target_integer_size;
		readonly uint target_long_integer_size;

		string[] argv;
		string[] envp;
		string working_directory;

		bool initialized;
		bool symtabs_read;

		int symtab_generation;
		ArrayList symtabs;

		public Debugger (string application, string[] arguments)
			: this (application, arguments, new SourceFileFactory ())
		{ }

		public Debugger (string application, string[] arguments, ISourceFileFactory source_factory)
		{
			NameValueCollection settings = ConfigurationSettings.AppSettings;

			foreach (string key in settings.AllKeys) {
				string value = settings [key];

				switch (key) {
				case "mono-path":
					Path_Mono = value;
					break;

				case "environment-path":
					Environment_Path = value;
					break;

				default:
					break;
				}
			}

			this.source_file_factory = source_factory;
			this.application = Assembly.LoadFrom (application);

			MethodInfo main = this.application.EntryPoint;
			string main_name = main.DeclaringType + ":" + main.Name;

			string[] argv = { Path_Mono, "--break", main_name, "--debug=mono",
					  "--noinline", "--nols", "--debug-args", "internal_mono_debugger",
					  application };
			string[] envp = { "PATH=" + Environment_Path };
			this.argv = argv;
			this.envp = envp;
			this.working_directory = ".";
		}

		//
		// IInferior
		//

		bool busy = false;
		public TargetState State {
			get {
				if (busy)
					return TargetState.BUSY;
				else if (inferior == null)
					return TargetState.NO_TARGET;
				else
					return inferior.State;
			}
		}

		bool DebuggerBusy {
			get {
				return busy;
			}

			set {
				if (busy == value)
					return;

				busy = value;
				if (StateChanged != null)
					StateChanged (State);
			}
		}

		void target_state_changed (TargetState new_state)
		{
			if (new_state == TargetState.STOPPED)
				frame_changed ();

			if (StateChanged != null)
				StateChanged (new_state);
		}

		public event TargetOutputHandler TargetOutput;
		public event TargetOutputHandler TargetError;
		public event StateChangedHandler StateChanged;

		//
		// IDebuggerBackend
		//

		public event StackFrameHandler FrameChangedEvent;
		public event StackFramesInvalidHandler FramesInvalidEvent;

		public IInferior Inferior {
			get {
				return inferior;
			}
		}

		void child_exited ()
		{
			inferior.Dispose ();
			inferior = null;
			initialized = false;
			symtabs_read = false;
		}

		void inferior_output (string line)
		{
			if (TargetOutput != null)
				TargetOutput (line);
		}

		void inferior_errors (string line)
		{
			if (TargetError != null)
				TargetError (line);
		}

		void do_update_symbol_files (object user_data, object result)
		{
			updating_symfiles = false;

			// Ooops, we received an old callback.
			if ((int) user_data < symtab_generation)
				return;

			// Nothing to do.
			if ((long) result == 0)
				return;

			DebuggerBusy = true;
			try {
				symtabs = do_update_symbol_files ();
			} catch {
				symtabs = null;
			} finally {
				DebuggerBusy = false;
				frame_changed ();
			}
		}

		ArrayList do_update_symbol_files ()
		{
			ArrayList symtabs = new ArrayList ();

			int header_size = 3 * inferior.TargetIntegerSize;

			ITargetLocation symbol_file_table = inferior.ReadAddress (
				inferior.MonoDebuggerInfo.symbol_file_table);

			ITargetMemoryReader header = inferior.ReadMemory (
				symbol_file_table, header_size);

			int size = header.ReadInteger ();
			int count = header.ReadInteger ();
			symtab_generation = header.ReadInteger ();

			ITargetMemoryReader symtab_reader = inferior.ReadMemory (
				symbol_file_table, size + header_size);
			symtab_reader.Offset = header_size;

			for (int i = 0; i < count; i++) {
				if (symtab_reader.ReadLongInteger () != OffsetTable.Magic)
					throw new SymbolTableException ();

				if (symtab_reader.ReadInteger () != OffsetTable.Version)
					throw new SymbolTableException ();

				int is_dynamic = symtab_reader.ReadInteger ();
				ITargetLocation image_file_addr = symtab_reader.ReadAddress ();
				string image_file = inferior.ReadString (image_file_addr);
				ITargetLocation raw_contents = symtab_reader.ReadAddress ();
				int raw_contents_size = symtab_reader.ReadInteger ();
				ITargetLocation address_table = symtab_reader.ReadAddress ();
				int address_table_size = symtab_reader.ReadInteger ();
				symtab_reader.ReadAddress ();

				if ((raw_contents_size == 0) || (address_table_size == 0)) {
					Console.WriteLine ("IGNORING SYMTAB");
					continue;
				}

				ITargetMemoryReader reader = inferior.ReadMemory
					(raw_contents, raw_contents_size);
				ITargetMemoryReader address_reader = inferior.ReadMemory
					(address_table, address_table_size);
				
				Console.WriteLine ("SYMTAB: {0:x} {1} - {2:x} {3} - {4:x} {5}",
						   raw_contents, raw_contents_size,
						   address_table, address_table_size,
						   image_file_addr, image_file);

				MonoSymbolTableReader symreader = new MonoSymbolTableReader (
					image_file, reader.BinaryReader, address_reader.BinaryReader);

				symtabs.Add (new CSharpSymbolTable (symreader, source_file_factory));
			}

			return symtabs;
		}

		bool updating_symfiles;
		void update_symbol_files ()
		{
			if ((inferior == null) || (inferior.MonoDebuggerInfo == null))
				return;

			if (updating_symfiles)
				return;

			updating_symfiles = true;

			int generation = inferior.ReadInteger (
				inferior.MonoDebuggerInfo.symbol_file_generation);
			if (generation == symtab_generation)
				return;

			inferior.call_method (inferior.MonoDebuggerInfo.update_symbol_file_table, 0,
					      new TargetAsyncCallback (do_update_symbol_files), generation);
		}

		public void Run ()
		{
			if (inferior != null)
				throw new TargetException ("Debugger already has an inferior.");

			inferior = new Inferior (working_directory, argv, envp);
			inferior.ChildExited += new ChildExitedHandler (child_exited);
			inferior.TargetOutput += new TargetOutputHandler (inferior_output);
			inferior.TargetError += new TargetOutputHandler (inferior_errors);
			inferior.StateChanged += new StateChangedHandler (target_state_changed);
		}

		public void Quit ()
		{
			if (inferior != null)
				inferior.Shutdown ();
		}

		public IStackFrame Frame ()
		{
			if (inferior == null)
				throw new NoTargetException ();

			ITargetLocation location = inferior.Frame ();

			update_symbol_files ();

			ISymbolHandle symbol_handle;
			ISourceLocation source = LookupAddress (location, out symbol_handle);

			if (source != null)
				return new StackFrame (inferior, location, source, symbol_handle);
			else
				return new StackFrame (inferior, location);
		}

		ISourceLocation LookupAddress (ITargetLocation address, out ISymbolHandle handle)
		{
			handle = null;
			if (symtabs == null)
				return null;

			foreach (ISymbolTable symtab in symtabs) {
				ISourceLocation source = symtab.Lookup (address, out handle);

				if (source != null)
					return source;
			}

			return null;
		}

		void frame_changed ()
		{
			IStackFrame frame = Frame ();

			if (FrameChangedEvent != null)
				FrameChangedEvent (frame);
		}

		public ISourceFileFactory SourceFileFactory {
			get {
				return source_file_factory;
			}

			set {
				source_file_factory = value;
			}
		}

		public IBreakPoint AddBreakPoint (ITargetLocation location)
		{
			throw new NotImplementedException ();
		}

		//
		// IDisposable
		//

		private bool disposed = false;

		protected virtual void Dispose (bool disposing)
		{
			// Check to see if Dispose has already been called.
			if (!this.disposed) {
				// If this is a call to Dispose,
				// dispose all managed resources.
				if (disposing) {
					// Do stuff here
					if (inferior != null)
						inferior.Kill ();
				}
				
				// Release unmanaged resources
				this.disposed = true;

				lock (this) {
					// Nothing to do yet.
				}
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			// Take yourself off the Finalization queue
			GC.SuppressFinalize (this);
		}

		~Debugger ()
		{
			Dispose (false);
		}
	}
}
