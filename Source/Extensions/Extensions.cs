#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Extensions
{
	internal static class Extensions
	{
		public static T? Choice<T>(this IReadOnlyCollection<T> self)
			where T : class?
		{
			int count = self.Count;
			if (count > 0) {
				int i = UnityEngine.Random.Range(0, count);
				return self.ElementAt(i);
			} else {
				return null;
			}
		}

		public static List<T> Clone<T>(this IReadOnlyList<T> self)
		{
			return new List<T>(self);
		}
	}
}
