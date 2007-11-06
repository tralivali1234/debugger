namespace Mono.Debugger.Languages
{
	public abstract class TargetGenericInstanceType : TargetStructType
	{
		protected TargetGenericInstanceType (Language language)
			: base (language, TargetObjectKind.GenericInstance)
		{ }

		public abstract TargetClassType ContainerType {
			get;
		}

	}
}
