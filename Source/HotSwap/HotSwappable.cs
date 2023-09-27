#nullable enable

using System;

namespace HotSwap
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class HotSwappableAttribute : Attribute { }
}
