using System;

using Mono.Debugger.Backend;

namespace Mono.Debugger.Architectures
{
	internal abstract class Instruction : DebuggerMarshalByRefObject
	{
		public enum Type
		{
			Unknown,
			Interpretable,
			ConditionalJump,
			IndirectCall,
			Call,
			IndirectJump,
			Jump,
			Ret
		}

		public enum TrampolineType
		{
			None,
			NativeTrampolineStart,
			NativeTrampoline,
			MonoTrampoline,
			DelegateInvoke
		}

		public abstract TargetAddress Address {
			get;
		}

		public abstract Type InstructionType {
			get;
		}

		public abstract bool IsIpRelative {
			get;
		}

		public bool IsCall {
			get {
				return (InstructionType == Type.Call) ||
					(InstructionType == Type.IndirectCall);
			}
		}

		public abstract bool HasInstructionSize {
			get;
		}

		public abstract int InstructionSize {
			get;
		}

		public abstract byte[] Code {
			get;
		}

		public abstract TargetAddress GetEffectiveAddress (TargetMemoryAccess memory);

		public abstract TrampolineType CheckTrampoline (TargetMemoryAccess memory,
								out TargetAddress trampoline);

		public abstract bool CanInterpretInstruction {
			get;
		}

		public abstract bool InterpretInstruction (Inferior inferior);
	}
}
