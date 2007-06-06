using System;
using System.Collections;
using Mono.Debugger.Backends;

namespace Mono.Debugger.Backends
{
	// Keep in sync with DebuggerRegisters in backends/server/x86-arch.h.
	internal enum X86_64_Register
	{
		R15		= 0,
		R14,
		R13,
		R12,
		RBP,
		RBX,
		R11,
		R10,
		R9,
		R8,
		RAX,
		RCX,
		RDX,
		RSI,
		RDI,
		ORIG_RAX,
		RIP,
		CS,
		EFLAGS,
		RSP,
		SS,
		FS_BASE,
		GS_BASE,
		DS,
		ES,
		GS,

		COUNT
	}

	// <summary>
	//   Architecture-dependent stuff for the x86_64.
	// </summary>
	internal class Architecture_X86_64 : Architecture
	{
		internal Architecture_X86_64 (ProcessServant process, TargetInfo info)
			: base (process, info)
		{ }

		internal override bool IsRetInstruction (TargetMemoryAccess memory,
							 TargetAddress address)
		{
			return memory.ReadByte (address) == 0xc3;
		}

		internal override bool IsSyscallInstruction (TargetMemoryAccess memory,
							     TargetAddress address)
		{
			try {
				return (memory.ReadByte (address - 2) == 0x0f) &&
					(memory.ReadByte (address - 1) == 0x05);
			} catch {
				return false;
			}
		}

		internal override TargetAddress GetCallTarget (TargetMemoryAccess target,
							       TargetAddress address,
							       out int insn_size)
		{
			if (address.Address == 0xffffe002) {
				insn_size = 0;
				return TargetAddress.Null;
			}

			TargetBinaryReader reader = target.ReadMemory (address, 6).GetReader ();

			byte opcode = reader.ReadByte ();
			byte original_opcode = opcode;

			if ((opcode == 0x48) || (opcode == 0x49))
				opcode = reader.ReadByte ();

			if (opcode == 0xe8) {
				int call_target = reader.ReadInt32 ();
				insn_size = 5;
				return address + reader.Position + call_target;
			} else if (opcode != 0xff) {
				insn_size = 0;
				return TargetAddress.Null;
			}

			byte address_byte = reader.ReadByte ();
			byte register;
			int disp;
			bool dereference_addr;

			if (((address_byte & 0x38) == 0x10) && ((address_byte >> 6) == 1)) {
				register = (byte) (address_byte & 0x07);
				disp = reader.ReadByte ();
				insn_size = 3;
				dereference_addr = true;
			} else if (((address_byte & 0x38) == 0x10) && ((address_byte >> 6) == 2)) {
				register = (byte) (address_byte & 0x07);
				disp = reader.ReadInt32 ();
				insn_size = 6;
				dereference_addr = true;
			} else if (((address_byte & 0x38) == 0x10) && ((address_byte >> 6) == 3)) {
				register = (byte) (address_byte & 0x07);
				disp = 0;
				insn_size = 2;
				dereference_addr = false;
			} else if (((address_byte & 0x38) == 0x10) && ((address_byte >> 6) == 0)) {
				register = (byte) (address_byte & 0x07);
				disp = 0;
				insn_size = 2;
				dereference_addr = true;
			} else {
				insn_size = 0;
				return TargetAddress.Null;
			}

			X86_64_Register reg;
			if (original_opcode == 0x49) {
				switch (register) {
				case 0: /* r8 */
					reg = X86_64_Register.R8;
					break;
				case 1: /* r9 */
					reg = X86_64_Register.R9;
					break;
				case 2: /* r10 */
					reg = X86_64_Register.R10;
					break;
				case 3: /* r11 */
					reg = X86_64_Register.R11;
					break;
				case 4: /* r12 */
					reg = X86_64_Register.R12;
					break;
				case 5: /* r13 */
					reg = X86_64_Register.R13;
					break;
				case 6: /* r14 */
					reg = X86_64_Register.R14;
					break;
				case 7: /* r15 */
					reg = X86_64_Register.R15;
					break;
				default:
					throw new InvalidOperationException ();
				}
			} else {
				switch (register) {
				case 0: /* rax */
					reg = X86_64_Register.RAX;
					break;
				case 1: /* rcx */
					reg = X86_64_Register.RCX;
					break;
				case 2: /* rdx */
					reg = X86_64_Register.RDX;
					break;
				case 3: /* rbx */
					reg = X86_64_Register.RBX;
					break;
				case 6: /* rsi */
					reg = X86_64_Register.RSI;
					break;
				case 7: /* rdi */
					reg = X86_64_Register.RDI;
					break;
				default:
					throw new InvalidOperationException ();
				}
			}

			if ((original_opcode == 0x48) || (original_opcode == 0x49))
				insn_size++;

			Registers regs = target.GetRegisters ();
			Register addr = regs [(int) reg];

			TargetAddress vtable_addr = new TargetAddress (AddressDomain, addr.Value);
			vtable_addr += disp;

			if (dereference_addr)
				return target.ReadAddress (vtable_addr);
			else
				return vtable_addr;
		}

		internal override TargetAddress GetJumpTarget (TargetMemoryAccess target,
							       TargetAddress address,
							       out int insn_size)
		{
			TargetBinaryReader reader = target.ReadMemory (address, 10).GetReader ();

			byte opcode = reader.ReadByte ();
			byte opcode2 = reader.ReadByte ();

			if ((opcode == 0xff) && (opcode2 == 0x25)) {
				insn_size = 6;
				int offset = reader.ReadInt32 ();
				return address + offset + 6;
			} else if ((opcode == 0xff) && (opcode2 == 0xa3)) {
				int offset = reader.ReadInt32 ();
				Registers regs = target.GetRegisters ();
				long rbx = regs [(int) X86_64_Register.RBX].Value;

				insn_size = 6;
				return new TargetAddress (AddressDomain, rbx + offset);
			}

			insn_size = 0;
			return TargetAddress.Null;
		}

		internal override TargetAddress GetTrampoline (TargetMemoryAccess target,
							       TargetAddress location,
							       TargetAddress trampoline_address)
		{
			if (trampoline_address.IsNull)
				return TargetAddress.Null;

			TargetBinaryReader reader = target.ReadMemory (location, 19).GetReader ();

			reader.Position = 9;

			byte opcode = reader.ReadByte ();
			if (opcode != 0x68)
				return TargetAddress.Null;

			int method_info = reader.ReadInt32 ();

			opcode = reader.ReadByte ();
			if (opcode != 0xe9)
				return TargetAddress.Null;

			int call_disp = reader.ReadInt32 ();

			if (location + call_disp + 19 != trampoline_address)
				return TargetAddress.Null;

			return new TargetAddress (AddressDomain, method_info);
		}

		public override string[] RegisterNames {
			get {
				return registers;
			}
		}

		public override int[] RegisterIndices {
			get {
				return important_regs;
			}
		}

		public override int[] AllRegisterIndices {
			get {
				return all_regs;
			}
		}

		public override int[] RegisterSizes {
			get {
				return reg_sizes;
			}
		}

		internal override int[] RegisterMap {
			get {
				return register_map;
			}
		}

		internal override int[] DwarfFrameRegisterMap {
			get {
				return dwarf_frame_register_map;
			}
		}

		internal override int CountRegisters {
			get {
				return (int) X86_64_Register.COUNT;
			}
		}

		int[] all_regs = { (int) X86_64_Register.R15,
				   (int) X86_64_Register.R14,
				   (int) X86_64_Register.R13,
				   (int) X86_64_Register.R12,
				   (int) X86_64_Register.RBP,
				   (int) X86_64_Register.RBX,
				   (int) X86_64_Register.R11,
				   (int) X86_64_Register.R10,
				   (int) X86_64_Register.R9,
				   (int) X86_64_Register.R8,
				   (int) X86_64_Register.RAX,
				   (int) X86_64_Register.RCX,
				   (int) X86_64_Register.RDX,
				   (int) X86_64_Register.RSI,
				   (int) X86_64_Register.RDI,
				   (int) X86_64_Register.ORIG_RAX,
				   (int) X86_64_Register.RIP,
				   (int) X86_64_Register.CS,
				   (int) X86_64_Register.EFLAGS,
				   (int) X86_64_Register.RSP,
				   (int) X86_64_Register.SS,
				   (int) X86_64_Register.FS_BASE,
				   (int) X86_64_Register.GS_BASE,
				   (int) X86_64_Register.DS,
				   (int) X86_64_Register.ES,
				   (int) X86_64_Register.GS };

		int[] important_regs = { (int) X86_64_Register.RBP,
					 (int) X86_64_Register.RBX,
					 (int) X86_64_Register.RAX,
					 (int) X86_64_Register.RCX,
					 (int) X86_64_Register.RDX,
					 (int) X86_64_Register.RSI,
					 (int) X86_64_Register.RDI,
					 (int) X86_64_Register.RIP,
					 (int) X86_64_Register.EFLAGS,
					 (int) X86_64_Register.RSP };

		// FIXME: Map mono/arch/amd64/amd64-codegen.h registers to
		//        debugger/arch/IArchitecture_X86_64.cs registers.
		int[] register_map = { (int) X86_64_Register.RAX, (int) X86_64_Register.RCX,
				       (int) X86_64_Register.RDX, (int) X86_64_Register.RBX,
				       (int) X86_64_Register.RSP, (int) X86_64_Register.RBP,
				       (int) X86_64_Register.RSI, (int) X86_64_Register.RDI,
				       (int) X86_64_Register.R8, (int) X86_64_Register.R9,
				       (int) X86_64_Register.R10, (int) X86_64_Register.R11,
				       (int) X86_64_Register.R12, (int) X86_64_Register.R13,
				       (int) X86_64_Register.R14, (int) X86_64_Register.R15,
				       (int) X86_64_Register.RIP };

		int[] dwarf_frame_register_map = new int[] {
			(int) X86_64_Register.RIP, (int) X86_64_Register.RSP, (int) X86_64_Register.RBP,

			(int) X86_64_Register.RAX, (int) X86_64_Register.RDX,
			(int) X86_64_Register.RCX, (int) X86_64_Register.RBX,
			(int) X86_64_Register.RSI, (int) X86_64_Register.RDI,
			(int) X86_64_Register.RBP, (int) X86_64_Register.RSP,
			(int) X86_64_Register.R8, (int) X86_64_Register.R9,
			(int) X86_64_Register.R10, (int) X86_64_Register.R11,
			(int) X86_64_Register.R12, (int) X86_64_Register.R13,
			(int) X86_64_Register.R14, (int) X86_64_Register.R15,
			(int) X86_64_Register.RIP
		};

		string[] registers = { "r15", "r14", "r13", "r12", "rbp", "rbx", "r11", "r10",
				       "r9", "r8", "rax", "rcx", "rdx", "rsi", "rdi", "orig_rax",
				       "rip", "cs", "eflags", "rsp", "ss", "fs_base", "gs_base",
				       "ds", "es", "gs" };

		int[] reg_sizes = { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
				    8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 };

		public override string PrintRegister (Register register)
		{
			if (!register.Valid)
				return "XXXXXXXXXXXXXXXX";

			switch ((X86_64_Register) register.Index) {
			case X86_64_Register.EFLAGS: {
				ArrayList flags = new ArrayList ();
				long value = register.Value;
				if ((value & (1 << 0)) != 0)
					flags.Add ("CF");
				if ((value & (1 << 2)) != 0)
					flags.Add ("PF");
				if ((value & (1 << 4)) != 0)
					flags.Add ("AF");
				if ((value & (1 << 6)) != 0)
					flags.Add ("ZF");
				if ((value & (1 << 7)) != 0)
					flags.Add ("SF");
				if ((value & (1 << 8)) != 0)
					flags.Add ("TF");
				if ((value & (1 << 9)) != 0)
					flags.Add ("IF");
				if ((value & (1 << 10)) != 0)
					flags.Add ("DF");
				if ((value & (1 << 11)) != 0)
					flags.Add ("OF");
				if ((value & (1 << 14)) != 0)
					flags.Add ("NT");
				if ((value & (1 << 16)) != 0)
					flags.Add ("RF");
				if ((value & (1 << 17)) != 0)
					flags.Add ("VM");
				if ((value & (1 << 18)) != 0)
					flags.Add ("AC");
				if ((value & (1 << 19)) != 0)
					flags.Add ("VIF");
				if ((value & (1 << 20)) != 0)
					flags.Add ("VIP");
				if ((value & (1 << 21)) != 0)
					flags.Add ("ID");
				string[] fstrings = new string [flags.Count];
				flags.CopyTo (fstrings, 0);
				return String.Join (" ", fstrings);
			}

			default:
				return String.Format ("{0:x}", register.Value);
			}
		}

		string format (Register register)
		{
			if (!register.Valid)
				return "XXXXXXXXXXXXXXXX";

			int bits = 16;
			string saddr = register.Value.ToString ("x");
			for (int i = saddr.Length; i < bits; i++)
				saddr = "0" + saddr;
			return saddr;
		}

		public override string PrintRegisters (StackFrame frame)
		{
			return PrintRegisters (frame.Registers);
		}

		public string PrintRegisters (Registers registers)
		{
			return String.Format (
				"RAX={0}  RBX={1}  RCX={2}  RDX={3}\n" +
				"RSI={4}  RDI={5}  RBP={6}  RSP={7}\n" +
				"R8 ={8}  R9 ={9}  R10={10}  R11={11}\n" +
				"R12={12}  R13={13}  R14={14}  R15={15}\n" +
				"RIP={16}  EFLAGS={17}\n",
				format (registers [(int) X86_64_Register.RAX]),
				format (registers [(int) X86_64_Register.RBX]),
				format (registers [(int) X86_64_Register.RCX]),
				format (registers [(int) X86_64_Register.RDX]),
				format (registers [(int) X86_64_Register.RSI]),
				format (registers [(int) X86_64_Register.RDI]),
				format (registers [(int) X86_64_Register.RBP]),
				format (registers [(int) X86_64_Register.RSP]),
				format (registers [(int) X86_64_Register.R8]),
				format (registers [(int) X86_64_Register.R9]),
				format (registers [(int) X86_64_Register.R10]),
				format (registers [(int) X86_64_Register.R11]),
				format (registers [(int) X86_64_Register.R12]),
				format (registers [(int) X86_64_Register.R13]),
				format (registers [(int) X86_64_Register.R14]),
				format (registers [(int) X86_64_Register.R15]),
				format (registers [(int) X86_64_Register.RIP]),
				PrintRegister (registers [(int) X86_64_Register.EFLAGS]));
		}

		internal override int MaxPrologueSize {
			get { return 50; }
		}

		internal override Registers CopyRegisters (Registers old_regs)
		{
			Registers regs = new Registers (old_regs);

			// According to the AMD64 ABI, rbp, rbx and r12-r15 are preserved
			// across function calls.
			regs [(int) X86_64_Register.RBX].Valid = true;
			regs [(int) X86_64_Register.RBP].Valid = true;
			regs [(int) X86_64_Register.R12].Valid = true;
			regs [(int) X86_64_Register.R13].Valid = true;
			regs [(int) X86_64_Register.R14].Valid = true;
			regs [(int) X86_64_Register.R15].Valid = true;

			return regs;
		}

		StackFrame unwind_method (StackFrame frame, TargetMemoryAccess memory, byte[] code,
					  int pos, int offset)
		{
			Registers old_regs = frame.Registers;
			Registers regs = CopyRegisters (old_regs);

			if (!old_regs [(int) X86_64_Register.RBP].Valid)
				return null;

			TargetAddress rbp = new TargetAddress (
				AddressDomain, old_regs [(int) X86_64_Register.RBP].Value);

			int addr_size = TargetAddressSize;
			TargetAddress new_rbp = memory.ReadAddress (rbp);
			regs [(int) X86_64_Register.RBP].SetValue (rbp, new_rbp);

			TargetAddress new_rip = memory.ReadAddress (rbp + addr_size);
			regs [(int) X86_64_Register.RIP].SetValue (rbp + addr_size, new_rip);

			TargetAddress new_rsp = rbp + 2 * addr_size;
			regs [(int) X86_64_Register.RSP].SetValue (rbp, new_rsp);

			rbp -= addr_size;

			int length = System.Math.Min (code.Length, offset);
			while (pos < length) {
				byte opcode = code [pos++];

				long value;
				if ((opcode == 0x41) && (pos < length)) {
					byte opcode2 = code [pos++];

					if ((opcode2 < 0x50) || (opcode2 > 0x57))
						break;

					switch (opcode2) {
					case 0x50: /* r8 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R8].SetValue (rbp, value);
						break;
					case 0x51: /* r9 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R9].SetValue (rbp, value);
						break;
					case 0x52: /* r10 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R10].SetValue (rbp, value);
						break;
					case 0x53: /* r11 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R11].SetValue (rbp, value);
						break;
					case 0x54: /* r12 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R12].SetValue (rbp, value);
						break;
					case 0x55: /* r13 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R13].SetValue (rbp, value);
						break;
					case 0x56: /* r14 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R14].SetValue (rbp, value);
						break;
					case 0x57: /* r15 */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.R15].SetValue (rbp, value);
						break;
					}
				} else {
					if ((opcode < 0x50) || (opcode > 0x57))
						break;

					switch (opcode) {
					case 0x50: /* rax */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RAX].SetValue (rbp, value);
						break;
					case 0x51: /* rcx */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RCX].SetValue (rbp, value);
						break;
					case 0x52: /* rdx */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RDX].SetValue (rbp, value);
						break;
					case 0x53: /* rbx */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RBX].SetValue (rbp, value);
						break;
					case 0x56: /* rsi */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RSI].SetValue (rbp, value);
						break;
					case 0x57: /* rdi */
						value = (long) memory.ReadInteger (rbp);
						regs [(int) X86_64_Register.RDI].SetValue (rbp, value);
						break;
					}
				}

				rbp -= addr_size;
			}

			return CreateFrame (frame.Thread, new_rip, new_rsp, new_rbp, regs);
		}

		StackFrame read_prologue (StackFrame frame, TargetMemoryAccess memory,
					  byte[] code, int offset)
		{
			int length = code.Length;
			int pos = 0;

			if (length < 4)
				return null;

			while ((pos < length) &&
			       (code [pos] == 0x90) || (code [pos] == 0xcc))
				pos++;

			if (pos+5 >= length) {
				// unknown prologue
				return null;
			}

			if (pos >= offset) {
				Registers regs = CopyRegisters (frame.Registers);

				TargetAddress new_rip = memory.ReadAddress (frame.StackPointer);
				regs [(int) X86_64_Register.RIP].SetValue (frame.StackPointer, new_rip);

				TargetAddress new_rsp = frame.StackPointer + TargetAddressSize;
				TargetAddress new_rbp = frame.FrameAddress;

				regs [(int) X86_64_Register.RSP].SetValue (new_rsp);

				return CreateFrame (frame.Thread, new_rip, new_rsp, new_rbp, regs);
			}

			// push %ebp
			if (code [pos++] != 0x55) {
				// unknown prologue
				return null;
			}

			if (pos >= offset) {
				Registers regs = CopyRegisters (frame.Registers);

				int addr_size = TargetAddressSize;
				TargetAddress new_rbp = memory.ReadAddress (frame.StackPointer);
				regs [(int) X86_64_Register.RBP].SetValue (frame.StackPointer, new_rbp);

				TargetAddress new_rsp = frame.StackPointer + addr_size;
				TargetAddress new_rip = memory.ReadAddress (new_rsp);
				regs [(int) X86_64_Register.RIP].SetValue (new_rsp, new_rip);
				new_rsp -= addr_size;

				regs [(int) X86_64_Register.RSP].SetValue (new_rsp);

				return CreateFrame (frame.Thread, new_rip, new_rsp, new_rbp, regs);
			}

			if (code [pos++] != 0x48) {
				// unknown prologue
				return null;
			}

			// mov %ebp, %esp
			if (((code [pos] != 0x8b) || (code [pos+1] != 0xec)) &&
			    ((code [pos] != 0x89) || (code [pos+1] != 0xe5))) {
				// unknown prologue
				return null;
			}

			pos += 2;
			if (pos >= offset)
				return null;

			return unwind_method (frame, memory, code, pos, offset);
		}

		internal override StackFrame UnwindStack (StackFrame frame, TargetMemoryAccess memory,
							  byte[] code, int offset)
		{
			if ((code != null) && (code.Length > 4))
				return read_prologue (frame, memory, code, offset);

			TargetAddress rbp = frame.FrameAddress;

			int addr_size = TargetAddressSize;

			Registers regs = CopyRegisters (frame.Registers);

			TargetAddress new_rbp = memory.ReadAddress (rbp);
			regs [(int) X86_64_Register.RBP].SetValue (rbp, new_rbp);

			TargetAddress new_rip = memory.ReadAddress (rbp + addr_size);
			regs [(int) X86_64_Register.RIP].SetValue (rbp + addr_size, new_rip);

			TargetAddress new_rsp = rbp + 2 * addr_size;
			regs [(int) X86_64_Register.RSP].SetValue (rbp, new_rsp);

			rbp -= addr_size;

			return CreateFrame (frame.Thread, new_rip, new_rsp, new_rbp, regs);
		}

		StackFrame try_unwind_sigreturn (StackFrame frame, TargetMemoryAccess memory)
		{
			byte[] data = memory.ReadMemory (frame.TargetAddress, 9).Contents;

			/*
			 * Check for signal return trampolines:
			 *
			 *   mov __NR_rt_sigreturn, %eax
			 *   syscall
			 */
			if ((data [0] != 0x48) || (data [1] != 0xc7) ||
			    (data [2] != 0xc0) || (data [3] != 0x0f) ||
			    (data [4] != 0x00) || (data [5] != 0x00) ||
			    (data [6] != 0x00) || (data [7] != 0x0f) ||
			    (data [8] != 0x05))
				return null;

			TargetAddress stack = frame.StackPointer;
			/* See `struct sigcontext' in <asm/sigcontext.h> */
			int[] regoffsets = {
				(int) X86_64_Register.R8,  (int) X86_64_Register.R9,
				(int) X86_64_Register.R10, (int) X86_64_Register.R11,
				(int) X86_64_Register.R12, (int) X86_64_Register.R13,
				(int) X86_64_Register.R14, (int) X86_64_Register.R15,
				(int) X86_64_Register.RDI, (int) X86_64_Register.RSI,
				(int) X86_64_Register.RBP, (int) X86_64_Register.RBX,
				(int) X86_64_Register.RDX, (int) X86_64_Register.RAX,
				(int) X86_64_Register.RCX, (int) X86_64_Register.RSP,
				(int) X86_64_Register.RIP, (int) X86_64_Register.EFLAGS
			};

			Registers regs = CopyRegisters (frame.Registers);

			int offset = 0x28;
			/* The stack contains the `struct ucontext' from <asm/ucontext.h>; the
			 * `struct sigcontext' starts at offset 0x28 in it. */
			foreach (int regoffset in regoffsets) {
				TargetAddress new_value = memory.ReadAddress (stack + offset);
				regs [regoffset].SetValue (new_value);
				offset += 8;
			}

			TargetAddress rip = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RIP].GetValue ());
			TargetAddress rsp = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RSP].GetValue ());
			TargetAddress rbp = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RBP].GetValue ());

			Symbol name = new Symbol ("<signal handler>", rip, 0);

			return new StackFrame (
				frame.Thread, rip, rsp, rbp, regs, frame.Thread.NativeLanguage, name);
		}

		internal override StackFrame TrySpecialUnwind (StackFrame frame,
							       TargetMemoryAccess memory)
		{
			StackFrame new_frame = try_unwind_sigreturn (frame, memory);
			if (new_frame != null)
				return new_frame;

			return null;
		}

		internal override void Hack_ReturnNull (Inferior inferior)
		{
			Registers regs = inferior.GetRegisters ();
			TargetAddress rsp = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RSP].GetValue ());
			TargetAddress rip = inferior.ReadAddress (rsp);
			rsp += TargetAddressSize;

			regs [(int) X86_64_Register.RIP].SetValue (rip);
			regs [(int) X86_64_Register.RSP].SetValue (rsp);
			regs [(int) X86_64_Register.RAX].SetValue (TargetAddress.Null);

			inferior.SetRegisters (regs);
		}

		protected override TargetAddress AdjustReturnAddress (Thread thread,
								      TargetAddress address)
		{
			TargetBinaryReader reader = thread.ReadMemory (address-7, 7).GetReader ();

			byte[] code = reader.ReadBuffer (7);
			if (code [2] == 0xe8)
				return address-5;

			if ((code [1] == 0xff) &&
			    ((code [2] & 0x38) == 0x10) && ((code [2] >> 6) == 2))
				return ((code [0] & 0x40) == 0x40) ? address-7 : address-6;
			else if ((code [4] == 0xff) &&
				 ((code [5] & 0x38) == 0x10) && ((code [5] >> 6) == 1))
				return ((code [3] & 0x40) == 0x40) ? address-4 : address-3;
			else if ((code [5] == 0xff) &&
				 ((code [6] & 0x38) == 0x10) && ((code [6] >> 6) == 3))
				return ((code [4] & 0x40) == 0x40) ? address-3 : address-2;
			else if ((code [5] == 0xff) &&
				 ((code [6] & 0x38) == 0x10) && ((code [6] >> 6) == 0))
				return ((code [4] & 0x40) == 0x40) ? address-3 : address-2;

			return address;
		}

		internal override StackFrame CreateFrame (Thread thread, Registers regs)
		{
			TargetAddress address = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RIP].GetValue ());
			TargetAddress stack_pointer = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RSP].GetValue ());
			TargetAddress frame_pointer = new TargetAddress (
				AddressDomain, regs [(int) X86_64_Register.RBP].GetValue ());

			return CreateFrame (thread, address, stack_pointer, frame_pointer, regs);
		}


		internal override StackFrame GetLMF (Thread thread)
		{
			TargetAddress lmf = thread.ReadAddress (thread.LMFAddress);

			TargetBinaryReader reader = thread.ReadMemory (lmf, 88).GetReader ();

			reader.Position = 32;

			TargetAddress rbx = reader.ReadTargetAddress ();
			TargetAddress rbp = reader.ReadTargetAddress ();
			TargetAddress rsp = reader.ReadTargetAddress ();
			TargetAddress r12 = reader.ReadTargetAddress ();
			TargetAddress r13 = reader.ReadTargetAddress ();
			TargetAddress r14 = reader.ReadTargetAddress ();
			TargetAddress r15 = reader.ReadTargetAddress ();

			Registers regs = new Registers (this);
			regs [(int) X86_64_Register.RBX].SetValue (lmf + 32, rbx);
			regs [(int) X86_64_Register.RBP].SetValue (lmf + 40, rbp);
			regs [(int) X86_64_Register.RSP].SetValue (lmf + 48, rsp);
			regs [(int) X86_64_Register.R12].SetValue (lmf + 56, r12);
			regs [(int) X86_64_Register.R13].SetValue (lmf + 64, r13);
			regs [(int) X86_64_Register.R14].SetValue (lmf + 72, r14);
			regs [(int) X86_64_Register.R15].SetValue (lmf + 80, r15);

			TargetAddress new_rip = thread.ReadAddress (rbp + 8);
			regs [(int) X86_64_Register.RIP].SetValue (rbp + 8, new_rip);

			TargetAddress new_rbp = thread.ReadAddress (rbp);
			regs [(int) X86_64_Register.RBP].SetValue (rbp, new_rbp);

			rbp -= 8;

			return CreateFrame (thread, new_rip, rsp, new_rbp, regs);
		}
	}
}
