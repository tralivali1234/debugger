using System;

namespace Mono.Debugger.Backends
{
	internal interface ILanguageBackend
	{
		string Name {
			get;
		}

		// <summary>
		//   The address of the JIT's generic trampoline code.
		// </summary>
		TargetAddress GenericTrampolineCode {
			get;
		}

		TargetAddress RuntimeInvokeFunc {
			get;
		}

		TargetAddress GetTrampoline (IInferior inferior, TargetAddress address);

		SourceMethod GetTrampoline (TargetAddress address);

		TargetAddress CompileMethod (IInferior inferior, TargetAddress method_address);
	}
}

