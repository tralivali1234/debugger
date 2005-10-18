using System;
using Mono.Debugger.Backends;

namespace Mono.Debugger.Languages.Mono
{
	internal class MonoClassObject : TargetClassObject
	{
		new MonoClassInfo type;

		public MonoClassObject (MonoClassInfo type, TargetLocation location)
			: base (type.Type, location)
		{
			this.type = type;
		}

		public override TargetClassObject GetParentObject (TargetAccess target)
		{
			if (!type.Type.HasParent)
				return null;

			return type.GetParentObject (target, Location);
		}

		public override TargetClassObject GetCurrentObject (TargetAccess target)
		{
			return type.GetCurrentObject (target, Location);
		}

		[Command]
		public override TargetObject GetField (TargetAccess target, TargetFieldInfo field)
		{
			return type.GetField (target, Location, field);
		}

		[Command]
		public override void SetField (TargetAccess target, TargetFieldInfo field,
					       TargetObject obj)
		{
			type.SetField (target, Location, field, obj);
		}

		internal TargetAddress KlassAddress {
			get { return type.KlassAddress; }
		}

		internal override long GetDynamicSize (TargetAccess target, TargetBlob blob,
						       TargetLocation location,
						       out TargetLocation dynamic_location)
		{
			throw new InvalidOperationException ();
		}

		public override string Print (TargetAccess target)
		{
			if (Location.HasAddress)
				return String.Format ("{0}", Location.Address);
			else
				return String.Format ("{0}", Location);
		}
	}
}
