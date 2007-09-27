using System;
using Mono.Debugger.Backends;
using Cecil = Mono.Cecil;

namespace Mono.Debugger.Languages.Mono
{
	internal class MonoObjectType : TargetObjectType
	{
		MonoSymbolFile file;
		Cecil.TypeDefinition typedef;
		MonoClassType class_type;

		protected MonoObjectType (MonoSymbolFile file, Cecil.TypeDefinition typedef, int size)
			: base (file.MonoLanguage, "object", size)
		{
			this.file = file;
			this.typedef = typedef;
		}

		public static MonoObjectType Create (MonoSymbolFile corlib, TargetMemoryAccess memory,
						     TargetReader mono_defaults)
		{
			int object_size = 2 * memory.TargetInfo.TargetAddressSize;

			MonoObjectType type = new MonoObjectType (
				corlib, corlib.ModuleDefinition.Types ["System.Object"],
				object_size);

			TargetAddress klass = mono_defaults.PeekAddress (
				corlib.MonoLanguage.MonoMetadataInfo.MonoDefaultsObjectOffset);
			type.create_type (memory, klass);

			return type;
		}

		protected void create_type (TargetMemoryAccess memory, TargetAddress klass)
		{
			class_type = file.MonoLanguage.CreateCoreType (file, typedef, memory, klass);
			file.MonoLanguage.AddCoreType (typedef, this, class_type, klass);
		}

		public MonoSymbolFile File {
			get { return file; }
		}

		public override bool HasClassType {
			get { return true; }
		}

		public override TargetClassType ClassType {
			get { return class_type; }
		}

		internal MonoClassType MonoClassType {
			get { return class_type; }
		}

		protected override TargetObject DoGetObject (TargetLocation location)
		{
			return new MonoObjectObject (this, location);
		}

		public override TargetPointerObject GetObject (TargetAddress address)
		{
			return new MonoObjectObject (this, new AbsoluteTargetLocation (address));
		}
	}
}
