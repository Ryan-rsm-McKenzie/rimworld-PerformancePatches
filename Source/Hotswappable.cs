using System;

namespace PerformancePatches
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class HotSwappableAttribute : Attribute
	{
	}
}
