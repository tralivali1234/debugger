using System;
using System.Collections;
using Mono.Debugger;
using Mono.Debugger.Languages;
using Mono.Debugger.Languages.Native;
using Mono.Debugger.Architecture;

namespace Mono.Debugger.Architecture
{
	internal class StabsReader : SymbolTable, ISymbolFile, ISimpleSymbolTable
	{
		protected Module module;
		protected ArrayList methods;
		protected Bfd bfd;
		protected SourceFileFactory factory;
		ArrayList files;
		Hashtable types;
		string filename;
		ISymbolRange[] ranges;
		protected TargetAddress entry_point = TargetAddress.Null;

		ObjectCache stabs_reader;
		ObjectCache stabstr_reader;

		public enum StabType : byte {
			N_FUN	= 0x24,
			N_MAIN	= 0x2a,
			N_RSYM	= 0x40,
			N_SLINE	= 0x44,
			N_SO	= 0x64,
			N_LSYM	= 0x80,
			N_BINCL	= 0x82,
			N_SOL	= 0x84,
			N_PSYM	= 0xa0,
			NEINCL	= 0xa2
		}

		public StabsReader (Bfd bfd, Module module, SourceFileFactory factory)
			: base (bfd.StartAddress, bfd.EndAddress)
		{
			this.bfd = bfd;
			this.module = module;
			this.factory = factory;
			this.filename = bfd.FileName;

			if (bfd.Target == "mach-o-be") {
				stabs_reader = create_reader ("LC_SYMTAB.stabs");
				stabstr_reader = create_reader ("LC_SYMTAB.stabstr");
			} else {
				stabs_reader = create_reader (".stab");
				stabstr_reader = create_reader (".stabstr");
			}

			TargetBinaryReader reader = StabTableReader;
			TargetBinaryReader string_reader = StringTableReader;

			types = new Hashtable ();

			string_type = new NativeStringType (0);
			int_type = RegisterFundamental (
				"int", typeof (int), 4);
			char_type = RegisterFundamental (
				"char", typeof (byte), 1);
			long_type = RegisterFundamental (
				"long int", typeof (int), 4);
			uint_type = RegisterFundamental (
				"unsigned int", typeof (uint), 4);
			ulong_type = RegisterFundamental (
				"long unsigned int", typeof (uint), 4);
			long_long_type = RegisterFundamental (
				"long long int", typeof (long), 4);
			ulong_long_type = RegisterFundamental (
				"long long unsigned int", typeof (ulong), 8);
			short_type = RegisterFundamental (
				"short int", typeof (short), 2);
			ushort_type = RegisterFundamental (
				"short unsigned int", typeof (ushort), 2);
			schar_type = RegisterFundamental (
				"signed char", typeof (sbyte), 1);
			uchar_type = RegisterFundamental (
				"unsigned char", typeof (byte), 1);
			float_type = RegisterFundamental (
				"float", typeof (float), 4);
			double_type = RegisterFundamental (
				"double", typeof (double), 8);

			files = new ArrayList ();
			methods = new ArrayList ();

			while (reader.Position < reader.Size) {
				Entry entry = new Entry (reader, string_reader);

				if (entry.n_type == (byte) StabType.N_SO) {
					FileEntry fentry = FileEntry.Create (
						this, reader, string_reader, ref entry);
					files.Add (fentry);
				}
			}

			ranges = new ISymbolRange [files.Count];
			for (int i = 0; i < files.Count; i++) {
				ranges [i] = new FileRangeEntry ((FileEntry) files [i]);
			}
		}

		NativeFundamentalType RegisterFundamental (string name, Type type, int size)
		{
			NativeFundamentalType native = new NativeFundamentalType (
				name, type, size);
			types.Add (name, native);
			return native;
		}

		NativeStringType string_type;
		NativeFundamentalType int_type;
		NativeFundamentalType char_type;
		NativeFundamentalType long_type;
		NativeFundamentalType uint_type;
		NativeFundamentalType ulong_type;
		NativeFundamentalType long_long_type;
		NativeFundamentalType ulong_long_type;
		NativeFundamentalType short_type;
		NativeFundamentalType ushort_type;
		NativeFundamentalType schar_type;
		NativeFundamentalType uchar_type;
		NativeFundamentalType float_type;
		NativeFundamentalType double_type;

		public static bool IsSupported (Bfd bfd)
		{
			if (bfd.Target == "elf32-i386")
				return bfd.HasSection (".stab");
			else if (bfd.Target == "mach-o-be")
				return bfd.HasSection ("LC_SYMTAB.stabs");
			else
				return false;
		}

		public TargetAddress EntryPoint {
			get { return entry_point; }
		}

		public override bool HasRanges {
			get { return true; }
		}

		public override ISymbolRange[] SymbolRanges {
			get { return ranges; }
		}

		public override bool HasMethods {
			get { return true; }
		}

		protected override ArrayList GetMethods ()
		{
			return methods;
		}

		public override Symbol SimpleLookup (TargetAddress address, bool exact_match)
		{
			if ((address < StartAddress) || (address >= EndAddress))
				return null;

			for (int i = methods.Count - 1; i >= 0; i--) {
				MethodEntry method = (MethodEntry) methods [i];

				if ((address < method.StartAddress) ||
				    (address > method.EndAddress))
					continue;

				long offset = address.Address - method.StartAddress.Address;
				if (offset == 0)
					return new Symbol (
						method.Name, method.StartAddress, 0);
				else if (exact_match)
					return null;
				else
					return new Symbol (
						method.Name, method.StartAddress, (int) offset);
			}

			return null;
		}

		public SourceFile[] Sources {
			get {
				SourceFile[] result = new SourceFile [files.Count];
				files.CopyTo (result, 0);
				return result;
			}
		}

		IMethod ISymbolFile.GetMethod (long handle)
		{
			return null;
		}

		void ISymbolFile.GetMethods (SourceFile file)
		{ }

		public SourceMethod FindMethod (string name)
		{
			return null;
		}

		protected struct Entry {
			public readonly string n_str;
			public readonly byte n_type;
			public readonly byte n_other;
			public readonly short n_ndesc;
			public readonly long n_value;

			public Entry (TargetBinaryReader reader, TargetBinaryReader str_reader)
			{
				int n_strx = reader.ReadInt32 ();
				if (n_strx > 0) {
					str_reader.Position = n_strx;
					n_str = str_reader.ReadString ();
				} else
					n_str = null;

				n_type = reader.ReadByte ();
				n_other = reader.ReadByte ();
				n_ndesc = reader.ReadInt16 ();
				n_value = reader.ReadAddress ();
			}

			public bool HasName {
				get { return (n_str != null) && (n_str != ""); }
			}

			public static byte PeekType (TargetBinaryReader reader)
			{
				return reader.PeekByte (reader.Position + 4);
			}

			public override string ToString ()
			{
				return String.Format ("Entry ({0}:{1:x}:{2:x}:{3:x}:{4})",
						      (StabType) n_type, n_other, n_ndesc,
						      n_value, n_str);
			}
		}

		protected MyVariable HandleSymbolOrType (ref Entry entry)
		{
			if (!entry.HasName)
				return null;

			Console.WriteLine ("   SYMBOL OR TYPE: {0}", entry.n_str);
			int pos = entry.n_str.IndexOf (':');
			string name = entry.n_str.Substring (0,pos);
			string def = entry.n_str.Substring (pos+1);

			if ((def [0] == 't') || (def [0] == 'T')) {
				int start = 1;
				ParseType (name, null, def, ref start);
				return null;
			} else if ((def [0] == '-') || (def [0] == '(')) {
				NativeType type = (NativeType) types [def];
				if (type == null)
					type = new NativeOpaqueType (null, 0);

				return new MyVariable (name, entry.n_value, type);
			}

			return null;
		}

		protected void RecordType (string name, NativeType type)
		{
			NativeType old = (NativeType) types [name];
			if (old == null) {
				types.Add (name, type);
				return;
			}

			NativeTypeAlias typedef = (NativeTypeAlias) old;
			Console.WriteLine ("TYPEDEF: {0}", typedef);
			typedef.TargetType = type;
		}

		protected NativeType ParseType (string name, string alias, string def,
						ref int pos)
		{
			Console.WriteLine ("PARSE TYPE: |{0}| - {1} - |{2}|{3}|",
					   name, pos, alias, def.Substring (pos));

			NativeType type;

			if (def [pos] == '(') {
				int new_pos = def.IndexOf (')', pos);
				alias = def.Substring (pos, new_pos-pos+1);
				pos = new_pos+1;

				if ((pos < def.Length) && (def [pos] == '=')) {
					pos++;
					type = ParseType (name, alias, def, ref pos);
					Console.WriteLine ("ALIAS: |{0}| - {1}", alias, type);
					RecordType (alias, type);
					return type;
				} else {
					type = (NativeType) types [alias];
					Console.WriteLine ("REFERENCE: |{0}| - {1}",
							   alias, type);
					return type;
				}
			}

			if (def [pos] == 'r')
				return ParseRange (name, alias, def, ref pos);
			else if (def [pos] == '*')
				return ParsePointer (name, alias, def, ref pos);
			else if (def [pos] == 'x') {
				int new_pos = def.IndexOf (':', pos+2);
				string reftype = def.Substring (pos+2, new_pos-pos-2);
				pos = new_pos+1;
				Console.WriteLine ("XREF: |{0}| - |{1}|", name, reftype);
				return new NativeTypeAlias (name, reftype);
			} else if (def [pos] == 's')
				return ParseStruct (name, def, ref pos);
			else if (def [pos] == 'a')
				return ParseArray (name, def, ref pos);

			Console.WriteLine ("UNKNOWN TYPE: |{0}|", def.Substring (pos));
			return null;
		}

		protected NativeType ParsePointer (string name, string alias, string def,
						   ref int pos)
		{
			pos++;
			NativeType type = ParseType (name, alias, def, ref pos);
			if (type == null)
				type = NativeType.VoidType;
			Console.WriteLine ("POINTER: {0} {1} {2}", name, alias, type);
			if (name == null)
				name = type.Name + " *";
			if (type == char_type)
				return string_type;
			else
				return new NativePointerType (name, type, 4);
		}

		protected NativeType ParseStruct (string name, string def, ref int pos)
		{
			int new_pos = pos+1;
			while ((def [new_pos] >= '0') && (def [new_pos] <= '9'))
				new_pos++;

			int size = (int) UInt32.Parse (def.Substring (pos+1,new_pos-pos-1));
			pos = new_pos;

			ArrayList members = new ArrayList ();

			Console.WriteLine ("STRUCT: {0} |{1}|", size, def.Substring (pos));
			int length = def.Length;
			while (pos < length) {
				if (def [pos] == ';') {
					pos++;
					break;
				}

				int p = def.IndexOf (':', pos);
				string mname = def.Substring (pos, p-pos);
				pos = p+1;

				Console.WriteLine ("MEMBER #0: {0} |{1}|", mname,
						   def.Substring (pos));

				NativeType type = ParseType (null, null, def, ref pos);

				Console.WriteLine ("MEMBER #1: {0} {1} |{2}|", mname, type,
						   def.Substring (pos));

				if (def [pos++] != ',') {
					Console.WriteLine ("UNKNOWN STRUCT DEF: |{0}|", def);
					return null;
				}

				p = def.IndexOf (',', pos);
				int moffs = (int) UInt32.Parse (def.Substring (pos, p-pos));
				pos = p+1;

				p = def.IndexOf (';', pos);
				int mbits = (int) UInt32.Parse (def.Substring (pos, p-pos));
				pos = p+1;

				if (type == null)
					type = NativeType.VoidType;

				Console.WriteLine ("MEMBER: {0} {1} {2} {3} - |{4}|",
						   mname, mbits, moffs, type,
						   def.Substring (pos));

				NativeFieldInfo field = new NativeFieldInfo (
					type, mname, members.Count, moffs, mbits);
				members.Add (field);
			}

			NativeFieldInfo[] fields = new NativeFieldInfo [members.Count];
			members.CopyTo (fields);

			return new NativeStructType (name, fields, size);
		}

		protected NativeType ParseArray (string name, string def, ref int pos)
		{
			pos++;
			NativeType index = ParseType (name, null, def, ref pos);
			Console.WriteLine ("ARRAY: {0} {1} - |{2}|", name, index,
					   def.Substring (pos));

			if (def [pos] == ';') {
				int p = def.IndexOf (';', pos+1);
				string lower = def.Substring (pos+1, p-pos-1);
				int q = def.IndexOf (';', p+1);
				string upper = def.Substring (p+1, q-p-1);
				pos = q+1;

				Console.WriteLine ("ARRAY #0: |{0}|{1}| - |{2}|",
						   lower, upper, def.Substring (pos));
			}

			NativeType element = ParseType (name, null, def, ref pos);
			Console.WriteLine ("ARRAY #1: {0} {1} - |{2}|", name, element,
					   def.Substring (pos));
			return null;
		}

		protected NativeType ParseRange (string name, string type, string def,
						 ref int pos)
		{
			int p = def.IndexOf (';', pos);
			string reftype = def.Substring (pos+1, p-pos-1);
			int q = def.IndexOf (';', p+1);
			string lower = def.Substring (p+1, q-p-1);
			int r = def.IndexOf (';', q+1);
			string upper = def.Substring (q+1, r-q-1);
			pos = r+1;

			Console.WriteLine ("RANGE: |{0}|{1}|{2}| - |{3}|", reftype,
					   lower, upper, def.Substring (pos));

			if (type == reftype)
				return (NativeType) types [name];
			else if (name == "float")
				return float_type;
			else if (name == "double")
				return double_type;

			return null;
		}

		protected class FileRangeEntry : SymbolRangeEntry
		{
			FileEntry file;

			public FileRangeEntry (FileEntry file)
				: base (file.StartAddress, file.EndAddress)
			{
				this.file = file;
			}

			protected override ISymbolLookup GetSymbolLookup ()
			{
				return file;
			}
		}

		protected class FileEntry : ISymbolLookup, ISymbolContainer {
			StabsReader stabs;
			public readonly ISourceBuffer SourceBuffer;
			public readonly SourceFile SourceFile;

			TargetAddress start, end;

			ArrayList lines = new ArrayList ();
			ArrayList methods = new ArrayList ();

			FileEntry (TargetBinaryReader reader, TargetBinaryReader str_reader,
				   StabsReader stabs, ref Entry entry, string name)
			{
				this.stabs = stabs;
				this.SourceFile = new SourceFile (stabs.module, name);

				start = stabs.bfd.GetAddress (entry.n_value);

				bool has_lines = false;

				while (reader.Position < reader.Size) {
					entry = new Entry (reader, str_reader);

				again:
					if (entry.n_type == (byte) StabType.N_SO) {
						end = stabs.bfd.GetAddress (entry.n_value);
						break;
					} else if (entry.n_type == (byte) StabType.N_SLINE) {
						LineNumberEntry lne = new LineNumberEntry (
							entry.n_ndesc, entry.n_value);
						lines.Add (lne);
						has_lines = true;
					} else if (entry.n_type == (byte) StabType.N_FUN) {
						MethodEntry mentry = new MethodEntry (
							this, reader, str_reader,
							ref entry, ref lines);
						methods.Add (mentry);
						if (mentry.Lines.Length > 0)
							has_lines = true;
						goto again;
					} else if (entry.n_type == (byte) StabType.N_LSYM) {
						stabs.HandleSymbolOrType (ref entry);
					}
				}

				if (end.IsNull)
					end = stabs.bfd.EndAddress;

				if (has_lines)
					SourceBuffer = stabs.factory.FindFile (name);
			}

			public static FileEntry Create (StabsReader stabs,
							TargetBinaryReader reader,
							TargetBinaryReader str_reader,
							ref Entry entry)
			{
				string name;
				if (entry.n_str.EndsWith ("/")) {
					string dir = entry.n_str;
					entry = new Entry (reader, str_reader);
					if (entry.n_str.StartsWith ("/"))
						name = entry.n_str;
					else
						name = dir + entry.n_str;
				} else
					name = entry.n_str;

				return new FileEntry (
					reader, str_reader, stabs, ref entry, name);
			}

			public StabsReader StabsReader {
				get { return stabs; }
			}

			bool ISymbolContainer.IsContinuous {
				get { return true; }
			}

			public TargetAddress StartAddress {
				get { return start; }
			}

			public TargetAddress EndAddress {
				get { return end; }
			}

			IMethod ISymbolLookup.Lookup (TargetAddress address)
			{
				foreach (MethodEntry method in methods) {
					if ((address >= method.StartAddress) &&
					    (address < method.EndAddress))
						return method;
				}

				return null;
			}
		}

		protected class MethodRange : SymbolRangeEntry
		{
			StabsReader stabs;

			public MethodRange (StabsReader stabs,
					    TargetAddress start, TargetAddress end)
				: base (start, end)
			{
				this.stabs = stabs;
			}

			protected override ISymbolLookup GetSymbolLookup ()
			{
				return stabs;
			}
		}

		protected class MethodSourceEntry : MethodSource {
			MethodEntry method;

			public MethodSourceEntry (MethodEntry method)
				: base (method, method.File.SourceFile)
			{
				this.method = method;
			}

			protected override MethodSourceData ReadSource ()
			{
				Bfd bfd = method.File.StabsReader.bfd;
				LineEntry[] addresses = new LineEntry [method.Lines.Length];
				for (int i = 0; i < addresses.Length; i++) {
					LineNumberEntry line = method.Lines [i];
					TargetAddress addr = bfd.GetAddress (line.Offset);
					addresses [i] = new LineEntry (addr, line.Line);
				}

				SourceMethod source = new SourceMethod (
					method.File.StabsReader.module, method.File.SourceFile,
					0, Name, StartRow, EndRow, false);

				return new MethodSourceData (
					method.StartLine, method.EndLine, addresses,
					source, method.File.SourceBuffer);
			}
		}

		protected class MethodEntry : MethodBase {
			public readonly FileEntry File;
			public readonly int StartLine, EndLine;
			public readonly LineNumberEntry[] Lines;
			public readonly MethodRange Range;
			ArrayList parameters, locals;

			static string GetName (string name)
			{
				int pos = name.IndexOf (':');
				if (pos < 0)
					return name;
				else
					return name.Substring (0, pos);
			}

			public MethodEntry (FileEntry file, TargetBinaryReader reader,
					    TargetBinaryReader str_reader,
					    ref Entry entry, ref ArrayList lines)
				: base (GetName (entry.n_str), file.StabsReader.FileName,
					file.StabsReader.module)
			{
				this.File = file;
				this.StartLine = entry.n_ndesc;

				long start_offset = entry.n_value;
				long end_offset = 0;

				StabsReader stabs = file.StabsReader;
				Bfd bfd = file.StabsReader.bfd;

				while (reader.Position < reader.Size) {
					entry = new Entry (reader, str_reader);

					if (entry.n_type == (byte) StabType.N_FUN) {
						end_offset = entry.n_value;

						if (!entry.HasName) {
							entry = new Entry (reader, str_reader);
							end_offset += start_offset;
						}

						break;
					} else if (entry.n_type == (byte) StabType.N_SLINE) {
						long value = entry.n_value + start_offset;
						LineNumberEntry lne = new LineNumberEntry (
							entry.n_ndesc, value);
						lines.Add (lne);
					} else if (entry.n_type == (byte) StabType.N_PSYM) {
#if FIXME
						byte next_type = Entry.PeekType (reader);
						MyVariable var;
						if (next_type == (byte) StabType.N_RSYM) {
							Entry next_entry = new Entry (
								reader, str_reader);
							var = new MyVariable (
								ref entry, ref next_entry);
						} else
							var = new MyVariable (ref entry);
						if (parameters == null)
							parameters = new ArrayList ();
						parameters.Add (var);
#endif
					} else if (entry.n_type == (byte) StabType.N_LSYM) {
						HandleSymbol (stabs, ref entry);
						continue;
#if FIXME
						byte next_type = Entry.PeekType (reader);
						MyVariable var;
						if (next_type == (byte) StabType.N_RSYM) {
							Entry next_entry = new Entry (
								reader, str_reader);
							var = new MyVariable (
								ref entry, ref next_entry);
						} else
							var = new MyVariable (ref entry);
						if (locals == null)
							locals = new ArrayList ();
						locals.Add (var);
#endif
					}
				}

				this.Lines = new LineNumberEntry [lines.Count];
				lines.CopyTo (Lines, 0);

				TargetAddress start = bfd.GetAddress (start_offset);
				TargetAddress end;
				if (end_offset > 0)
					end = bfd.GetAddress (end_offset);
				else
					end = bfd.EndAddress;

				SetAddresses (start, end);

				if (Lines.Length > 0) {
					LineNumberEntry first = Lines [0];
					LineNumberEntry last = Lines [Lines.Length -1];

					TargetAddress mstart = bfd.GetAddress (first.Offset);
					TargetAddress mend = bfd.GetAddress (last.Offset);

					SetMethodBounds (mstart, mend);

					EndLine = last.Line;
				} else
					EndLine = StartLine;

				this.Range = new MethodRange (file.StabsReader, start, end);
				SetSource (new MethodSourceEntry (this));

				if (Name == "main")
					file.StabsReader.entry_point = start;

				file.StabsReader.methods.Add (this);

				lines = new ArrayList ();
			}

			protected void HandleSymbol (StabsReader stabs, ref Entry entry)
			{
				MyVariable var = stabs.HandleSymbolOrType (ref entry);
				if (var == null)
					return;

				if (locals == null)
					locals = new ArrayList ();

				locals.Add (var);
			}

			public override object MethodHandle {
				get { return null; }
			}

			public override ITargetStructType DeclaringType {
				get { return null; }
			}

			public override bool HasThis {
				get { return false; }
			}

			public override IVariable This {
				get {
					throw new InvalidOperationException ();
				}
			}

			public override IVariable[] Parameters {
				get {
					if (parameters == null)
						return new IVariable [0];

					IVariable[] result = new IVariable [parameters.Count];
					parameters.CopyTo (result);
					return result;
				}
			}

			public override IVariable[] Locals {
				get {
					if (locals == null)
						return new IVariable [0];

					IVariable[] result = new IVariable [locals.Count];
					locals.CopyTo (result);
					return result;
				}
			}

			public override SourceMethod GetTrampoline (ITargetMemoryAccess memory,
								    TargetAddress address)
			{
				return null;
			}

			public override string ToString ()
			{
				return String.Format ("Method ({0}:{1}:{2:x})", Name,
						      StartLine, StartAddress);
			}
		}

		protected class MyVariable : IVariable
		{
			string name;
			NativeType type;
			long offset;
			int register;

			static string GetName (string name)
			{
				int pos = name.IndexOf (':');
				if (pos < 0)
					return name;
				else
					return name.Substring (0, pos);
			}

			public MyVariable (string name, long offset, NativeType type)
			{
				this.name = name;
				this.type = type;

				this.offset = offset;
				this.register = -1;

				Console.WriteLine (this);
			}

			public string Name {
				get { return name; }
			}

			public ITargetType Type {
				get { return type; }
			}

			public bool IsAlive (TargetAddress address)
			{
				// FIXME
				return true;
			}

			public bool CheckValid (StackFrame frame)
			{
				// FIXME
				return true;
			}

			protected TargetLocation GetAddress (StackFrame frame)
			{
				return new MonoVariableLocation (
					frame, true, (int) I386Register.EBP, offset,
					type.IsByRef, 0);
			}

			public ITargetObject GetObject (StackFrame frame)
			{
				TargetLocation location = GetAddress (frame);
				if (location == null)
					return null;

				return type.GetObject (location);
			}

			public bool CanWrite {
				get { return false; }
			}

			public void SetObject (StackFrame frame, ITargetObject obj)
			{
				throw new InvalidOperationException ();
			}

			public override string ToString ()
			{
				return String.Format ("NativeVariable [{0}:{1}:{2}:{3:x}]",
						      Name, Type, register, offset);
			}
		}

		protected struct LineNumberEntry {
			public readonly int Line;
			public readonly long Offset;

			public LineNumberEntry (int line, long offset)
			{
				this.Line = line;
				this.Offset = offset;
			}

			public override string ToString ()
			{
				return String.Format ("Line ({0}:{1:x})", Line, Offset);
			}
		}

		public TargetBinaryReader StabTableReader {
			get {
				return new TargetBinaryReader (
					(TargetBlob) stabs_reader.Data, bfd.TargetInfo);
			}
		}

		public TargetBinaryReader StringTableReader {
			get {
				return new TargetBinaryReader (
					(TargetBlob) stabstr_reader.Data, bfd.TargetInfo);
			}
		}

		public void Read ()
		{ }

		public string FileName {
			get { return filename; }
		}

		object create_reader_func (object user_data)
		{
			byte[] contents = bfd.GetSectionContents ((string) user_data, true);
			if (contents == null)
				throw new StabsException (
					this, "Can't find stabs debugging info");

			return new TargetBlob (contents);
		}

		ObjectCache create_reader (string section_name)
		{
			return new ObjectCache (
				new ObjectCacheFunc (create_reader_func), section_name, 5);
		}

		protected class StabsException : Exception
		{
			public StabsException (StabsReader reader, string message)
				: base (String.Format ("{0}: {1}", reader.FileName, message))
			{ }

			public StabsException (StabsReader reader, string message,
					       Exception inner)
				: base (String.Format ("{0}: {1}", reader.FileName, message),
					inner)
			{ }
		}
	}
}
