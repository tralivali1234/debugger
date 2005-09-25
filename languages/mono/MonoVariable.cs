using System;
using Mono.Debugger.Backends;

namespace Mono.Debugger.Languages.Mono
{
	internal class MonoVariable : MarshalByRefObject, IVariable
	{
		VariableInfo info;
		string name;
		TargetType type;
		DebuggerBackend backend;
		TargetAddress start_liveness, end_liveness;
		TargetAddress start_scope, end_scope;
		bool has_liveness_info, is_byref;

		public MonoVariable (DebuggerBackend backend, string name, TargetType type,
				     bool is_local, bool is_byref, IMethod method,
				     VariableInfo info, int start_scope_offset,
				     int end_scope_offset)
			: this (backend, name, type, is_local, is_byref, method, info)
		{
			if (is_local) {
				start_scope = method.StartAddress + start_scope_offset;
				end_scope = method.StartAddress + end_scope_offset;
			} else if (method.HasMethodBounds) {
				start_scope = method.MethodStartAddress;
				end_scope = method.MethodEndAddress;
			} else {
				start_scope = method.StartAddress;
				end_scope = method.EndAddress;
			}

			if (has_liveness_info) {
				if (start_liveness < start_scope)
					start_liveness = start_scope;
				if (end_liveness > end_scope)
					end_liveness = end_scope;
			} else {
				start_liveness = start_scope;
				end_liveness = end_scope;
				has_liveness_info = true;
			}
		}

		public MonoVariable (DebuggerBackend backend, string name, TargetType type,
				     bool is_local, bool is_byref, IMethod method,
				     VariableInfo info)
		{
			this.backend = backend;
			this.name = name;
			this.type = type;
			this.info = info;
			this.is_byref = is_byref;

			if (info.HasLivenessInfo) {
				start_liveness = method.StartAddress + info.BeginLiveness;
				end_liveness = method.StartAddress + info.EndLiveness;
				has_liveness_info = true;
			} else {
				start_liveness = method.MethodStartAddress;
				end_liveness = method.MethodEndAddress;
				has_liveness_info = false;
			}
		}

		public DebuggerBackend Backend {
			get { return backend; }
		}

		public string Name {
			get { return name; }
		}

		public ITargetType Type {
			get { return type; }
		}

		public TargetAddress StartLiveness {
			get { return start_liveness; }
		}

		public TargetAddress EndLiveness {
			get { return end_liveness; }
		}

		public TargetLocation GetLocation (StackFrame frame)
		{
			Register register = frame.Registers [info.Index];
			if (info.Mode == VariableInfo.AddressMode.Register)
				return new MonoVariableLocation (
					frame.TargetAccess, false, register, info.Offset, is_byref);
			else if (info.Mode == VariableInfo.AddressMode.RegOffset)
				return new MonoVariableLocation (
					frame.TargetAccess, true, register, info.Offset, is_byref);
			else
				return null;
		}

		public bool IsAlive (TargetAddress address)
		{
			return (address >= start_liveness) && (address <= end_liveness);
		}

		public ITargetObject GetObject (StackFrame frame)
		{
			TargetLocation location = GetLocation (frame);

			if (location == null)
				throw new LocationInvalidException ();

			if (location.HasAddress && location.Address.IsNull)
				return backend.MonoLanguage.CreateNullObject (
					frame.TargetAccess, type);

			return type.GetObject (location);
		}

		public bool CanWrite {
			get { return true; }
		}

		public void SetObject (StackFrame frame, ITargetObject obj)
		{
			TargetLocation location = GetLocation (frame);

			if (location == null)
				throw new LocationInvalidException ();

			type.SetObject (frame.TargetAccess, location, (TargetObject) obj);
		}

		public override string ToString ()
		{
			return String.Format ("MonoVariable [{0}:{1}:{2}:{3}]",
					      Name, Type, StartLiveness, EndLiveness);
		}
	}
}