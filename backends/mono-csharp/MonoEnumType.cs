using System;
using Mono.Debugger.Backends;

namespace Mono.Debugger.Languages.CSharp
{
	internal class MonoEnumType : MonoFundamentalType
	{
		MonoType element_type;

		public MonoEnumType (Type type, int size, TargetAddress klass,
				     TargetBinaryReader info, MonoSymbolTable table)
			: base (type, size, klass, info, table)
		{
			int element_type_info = info.ReadInt32 ();
			element_type = GetType (type.GetElementType (), element_type_info, table);
		}

		public static bool Supports (Type type, TargetBinaryReader info)
		{
			return type.IsEnum;
		}

		public override int Size {
			get {
				return element_type.Size;
			}
		}

		public override bool IsByRef {
			get {
				return false;
			}
		}

		public override MonoObject GetObject (TargetLocation location)
		{
			MonoObject obj = element_type.GetObject (location);
			return new MonoEnumObject (this, location, (MonoFundamentalObjectBase) obj);
		}
	}
}
