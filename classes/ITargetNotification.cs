using System;
using System.IO;

namespace Mono.Debugger
{
	public delegate void TargetOutputHandler (bool is_stderr, string output);
	public delegate void DebuggerOutputHandler (string output);
	public delegate void DebuggerErrorHandler (object sender, string message, Exception e);
	public delegate void StateChangedHandler (TargetState new_state, int arg);

	/// <summary>
	///   State of the target (the application we're debugging).
	/// </summary>
	public enum TargetState
	{
		// <summary>
		//   There is no target to debug.
		// </summary>
		NoTarget,

		// <summary>
		//   The debugger is busy doing some things.
		// </summary>
		Busy,

		// <summary>
		//   The target is running.
		// </summary>
		Running,

		// <summary>
		//   The target is stopped.
		// </summary>
		Stopped,

		// <summary>
		//   The target has exited.
		// </summary>
		Exited,

		// <summary>
		//   Undebuggable daemon thread.
		// </summary>
		Daemon,

		// <summary>
		//   This is a core file.
		// </summary>
		CoreFile,

		LAST
	}

	public interface ITargetNotification
	{
		// <summary>
		//   Get the state of the target we're debugging.
		// </summary>
		TargetState State {
			get;
		}

		// <summary>
		//   This event is emitted when the state of the target we're currently debugging
		//   has changed, for instance when the target has stopped or exited.
		// </summary>
		event StateChangedHandler StateChanged;
	}
}
