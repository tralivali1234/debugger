using System;

namespace Mono.Debugger.Languages.Native
{
	internal class NativePointerType : TargetPointerType
	{
		public NativePointerType (ILanguage language, string name, int size)
			: base (language, name, size)
		{ }

		public NativePointerType (ILanguage language, string name,
					  TargetType target_type, int size)
			: this (language, name, size)
		{
			this.target_type = target_type;
		}

		TargetType target_type;

		public override bool IsTypesafe {
			get { return false; }
		}

		public override bool HasStaticType {
			get { return target_type != null; }
		}

		public override bool IsArray {
			get { return true; }
		}

		public override TargetType StaticType {
			get {
				if (target_type == null)
					throw new InvalidOperationException ();

				return target_type;
			}
		}

		internal override TargetObject GetObject (TargetLocation location)
		{
			return new NativePointerObject (this, location);
		}

		public override string ToString ()
		{
			return String.Format ("{0} [{1}:{2}:{3}]", GetType (),
					      Name, Size, target_type);
		}
	}
}