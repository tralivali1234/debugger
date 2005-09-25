using System;

namespace Mono.Debugger.Languages
{
	public interface ILanguage : IDisposable
	{
		string Name {
			get;
		}

		ITargetInfo TargetInfo {
			get;
		}

		TargetFundamentalType IntegerType {
			get;
		}

		TargetFundamentalType LongIntegerType {
			get;
		}

		TargetFundamentalType StringType {
			get;
		}

		ITargetType PointerType {
			get;
		}

		ITargetType VoidType {
			get;
		}

		TargetClassType DelegateType {
			get;
		}

		TargetClassType ExceptionType {
			get;
		}

		string SourceLanguage (StackFrame frame);

		ITargetType LookupType (StackFrame frame, string name);

		bool CanCreateInstance (Type type);

		ITargetObject CreateInstance (StackFrame frame, object obj);

		TargetFundamentalObject CreateInstance (ITargetAccess target, int value);

		TargetPointerObject CreatePointer (StackFrame frame, TargetAddress address);

		ITargetObject CreateObject (ITargetAccess target, TargetAddress address);

		ITargetObject CreateNullObject (ITargetAccess target, ITargetType type);

		TargetAddress AllocateMemory (ITargetAccess target, int size);
	}
}

