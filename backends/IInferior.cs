using System;
using System.IO;
using Mono.Debugger.Architecture;

namespace Mono.Debugger.Backends
{
	public interface IInferiorStackFrame
	{
		TargetAddress Address {
			get;
		}
	}

	public enum ChildEventType {
		CHILD_EXITED = 1,
		CHILD_STOPPED,
		CHILD_SIGNALED,
		CHILD_CALLBACK,
		CHILD_HIT_BREAKPOINT,
		CHILD_MEMORY_CHANGED
	}

	public delegate void ChildEventHandler (ChildEventType message, int arg);

	public sealed class ChildEvent
	{
		public readonly ChildEventType Type;
		public readonly int Argument;

		public readonly long Callback;
		public readonly long Data1;
		public readonly long Data2;

		public ChildEvent (ChildEventType type, int arg)
		{
			this.Type = type;
			this.Argument = arg;
			this.Callback = this.Data1 = this.Data2 = 0;
		}

		public ChildEvent (long callback, long data, long data2)
		{
			this.Type = ChildEventType.CHILD_CALLBACK;
			this.Argument = 0;

			this.Callback = callback;
			this.Data1 = data;
			this.Data2 = data2;
		}
	}

	internal interface IInferior : ITargetAccess, ITargetNotification, IDisposable
	{
		/// <summary>
		///   Start the target.
		/// </summary>
		/// <remarks>
		///   Some of the other methods and properties of this interface may only
		///   by used in the thread which called this function !
		/// </remarks>
		void Run (bool redirect_fds);
		void Attach (int pid);

		/// <summary>
		///   Continue the target.
		/// </summary>
		void Continue ();

		/// <summary>
		///   Aborts the target being debugged, but gives it time to terminate cleanly.
		///   On Unix systems, this'll send a SIGTERM to the target process.
		/// </summary>
		void Shutdown ();

		/// <summary>
		///   Forcibly kills the target without giving it any time to terminate.
		///   On Unix systems, this'll send a SIGKILL to the target process.
		/// </summary>
		void Kill ();

		/// <summary>
		///   Get the current target location.
		/// </summary>
		TargetAddress CurrentFrame {
			get;
		}

		/// <summary>
		///   Whether the user set a breakpoint at the current instruction.
		/// </summary>
		/// <remarks>
		///   This method only checks whether the user set a breakpoint at the
		///   current instruction, it does not track breakpoint instruction which
		///   were already in the source code.
		/// </remarks>
		bool CurrentInstructionIsBreakpoint {
			get;
		}

		/// <summary>
		///   Single-step one instruction.
		/// </summary>
		void Step ();

		/// <summary>
		///   Stop the target.
		/// </summary>
		void Stop ();

		/// <summary>
		///   Sets the signal to be sent to the target the next time
		///   it is resumed.
		/// </summary>
		void SetSignal (int signal, bool send_it);

		/// <remarks>
		///   The following two methods are more or less private.
		/// </remarks>
		long CallMethod (TargetAddress method, long method_argument1, long method_argument2);
		long CallStringMethod (TargetAddress method, long method_argument,
				       string string_argument);
		void RuntimeInvoke (TargetAddress invoke_method, TargetAddress method_argument,
				    TargetAddress object_argument, TargetAddress[] param_objects);
		TargetAddress SimpleLookup (string name);

		ChildEvent Wait ();

		void UpdateModules ();

		long[] GetRegisters (int[] registers);
		TargetAddress GetReturnAddress ();

		void SetRegister (int register, long value);
		void SetRegisters (int[] registers, long[] values);

		IInferiorStackFrame[] GetBacktrace (int max_frames, TargetAddress stop);

		TargetMemoryArea[] GetMemoryMaps ();

		int InsertBreakpoint (TargetAddress address);

		int InsertHardwareBreakpoint (TargetAddress address, int index);

		void RemoveBreakpoint (int breakpoint);

		void EnableBreakpoint (int breakpoint);

		void DisableBreakpoint (int breakpoint);

		int PID {
			get;
		}

		Bfd Bfd {
			get;
		}

		TargetAddress MainMethodAddress {
			get;
		}

		/// <summary>
		///   Returns a disassembler for the current target.
		/// </summary>
		IDisassembler Disassembler {
			get;
		}

		/// <summary>
		///   Gets the IArchitecture for the current target.
		/// </summary>
		IArchitecture Architecture {
			get;
		}

		DebuggerBackend DebuggerBackend {
			get;
		}

		Module[] Modules {
			get;
		}

		SingleSteppingEngine SingleSteppingEngine {
			get; set;
		}
		
		event ChildEventHandler ChildEvent;

		// <summary>
		//   This event is emitted when the target we're currently debugging has sent any
		//   output to stdout or stderr.
		// </summary>
		event TargetOutputHandler TargetOutput;

		// <summary>
		//   This event is emitted by the debugger to write diagnostic messages and errors.
		// </summary>
		event DebuggerOutputHandler DebuggerOutput;

		event DebuggerErrorHandler DebuggerError;

		int SIGKILL {
			get;
		}
		int SIGSTOP {
			get;
		}
		int SIGINT {
			get;
		}
		int SIGCHLD {
			get;
		}
		int SIGPROF {
			get;
		}
		int SIGPWR {
			get;
		}
		int SIGXCPU {
			get;
		}
		int ThreadAbortSignal {
			get;
		}
		int ThreadRestartSignal {
			get;
		}
		int ThreadDebugSignal {
			get;
		}
		int MonoThreadDebugSignal {
			get;
		}
	}
}

