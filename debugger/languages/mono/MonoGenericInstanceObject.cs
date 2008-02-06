using System;
using System.Text;
using Cecil = Mono.Cecil;

namespace Mono.Debugger.Languages.Mono
{
	internal class MonoGenericInstanceObject : TargetGenericInstanceObject
	{
		new MonoGenericInstanceType type;
		MonoClassInfo info;

		public MonoGenericInstanceObject (MonoGenericInstanceType type,
						  MonoClassInfo info, TargetLocation location)
			: base (type, location)
		{
			this.type = type;
			this.info = info;
		}

		internal override TargetStructObject GetParentObject (TargetMemoryAccess target)
		{
			if (!type.HasParent || !type.IsByRef)
				return null;

			TargetStructType sparent = type.GetParentType (target);
			if (sparent == null)
				return null;

			return (TargetStructObject) sparent.GetObject (target, Location);
		}

		internal override TargetStructObject GetCurrentObject (TargetMemoryAccess target)
		{
			if (!type.IsByRef)
				return null;

			return type.GetCurrentObject (target, Location);
		}

		internal override long GetDynamicSize (TargetMemoryAccess target, TargetBlob blob,
						       TargetLocation location,
						       out TargetLocation dynamic_location)
		{
			throw new InvalidOperationException ();
		}

		internal override string Print (TargetMemoryAccess target)
		{
			if (Location.HasAddress)
				return String.Format ("({0}) {1}",
						      Type.Name, Location.GetAddress (target));
			else
				return String.Format ("({0}) {1}",
						      Type.Name, Location);
		}
	}
}
