using System;
using System.Collections.Generic;

namespace PerformancePatches
{
	internal static class IEnumerableExt
	{
		public static bool Empty<T>(this IEnumerable<T> self)
		{
			return !self.GetEnumerator().MoveNext();
		}

		public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source)
		{
			foreach (var outer in source) {
				foreach (var inner in outer) {
					yield return inner;
				}
			}
		}

		public static void ForEach<T>(this IEnumerable<T> self, Action<T> f)
		{
			foreach (var elem in self) {
				f(elem);
			}
		}

		public static IEnumerable<TResult> SelectNotNull<TSource, TResult>(this IEnumerable<TSource> self, Func<TSource, TResult> f)
		{
			foreach (var elem in self) {
				var result = f(elem);
				if (result != null) {
					yield return result;
				}
			}
		}
	}

	internal static class ListExt
	{
		public static List<T> Clone<T>(this List<T> list)
		{
			return new List<T>(list);
		}
	}
}
