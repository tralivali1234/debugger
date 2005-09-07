using System;

namespace Mono.Debugger.Languages.Mono
{
	internal class MonoOpaqueType : MonoType
	{
		public MonoOpaqueType (MonoSymbolFile file, Type type)
			: base (file, TargetObjectKind.Opaque, type)
		{ }

		public override bool IsByRef {
			get { return false; }
		}

		public override bool HasFixedSize {
			get { return true; }
		}

		protected override IMonoTypeInfo DoGetTypeInfo (TargetBinaryReader info)
		{
			return null;
		}
	}
}
