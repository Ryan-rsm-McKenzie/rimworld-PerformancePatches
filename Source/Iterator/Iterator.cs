#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Iterator
{
	internal static class ICollectionExt
	{
		public static bool IsEmpty<T>(this ICollection<T> self) => self.Count == 0;
	}

	internal static class IEnumerableExt
	{
		public static IEnumerable<T> Chain<T>(this IEnumerable<T> self, IEnumerable<T> other)
		{
			return self.Concat(other);
		}

		public static IEnumerable<T> Cycle<T>(this IEnumerable<T> self)
		{
			return new Cycle<T>(self);
		}

		public static IEnumerable<(int Enumerator, T Item)> Enumerate<T>(this IEnumerable<T> self)
		{
			int i = 0;
			foreach (var elem in self) {
				yield return (i++, elem);
			}
		}

		public static IEnumerable<T> Filter<T>(this IEnumerable<T> self, Func<T, bool> predicate)
		{
			return self.Where(predicate);
		}

		public static IEnumerable<U> FilterMap<T, U>(this IEnumerable<T> self, Func<T, U?> f, U? _ = null)
			where U : class?
		{
			foreach (var elem in self) {
				var result = f(elem);
				if (result is not null) {
					yield return result;
				}
			}
		}

		public static IEnumerable<U> FilterMap<T, U>(this IEnumerable<T> self, Func<T, U?> f, U? _ = null)
			where U : struct
		{
			foreach (var elem in self) {
				var result = f(elem);
				if (result.HasValue) {
					yield return result.Value;
				}
			}
		}

		public static T? Find<T>(this IEnumerable<T> self, Func<T, bool> predicate, T? _ = null)
			where T : class?
		{
			foreach (var elem in self) {
				if (predicate(elem)) {
					return elem;
				}
			}

			return null;
		}

		public static T? Find<T>(this IEnumerable<T> self, Func<T, bool> predicate, T? _ = null)
			where T : struct
		{
			foreach (var elem in self) {
				if (predicate(elem)) {
					return elem;
				}
			}

			return null;
		}

		public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> self)
		{
			foreach (var outer in self) {
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

		public static IEnumerable<T> Intersperse<T>(this IEnumerable<T> self, T separator)
		{
			var iter = self.GetEnumerator();
			if (iter.MoveNext()) {
				var current = iter.Current;
				while (iter.MoveNext()) {
					yield return current;
					yield return separator;
					current = iter.Current;
				}
				yield return current;
			}
		}

		public static IEnumerable<T> IntersperseWith<T>(this IEnumerable<T> self, int count, Func<T> separator)
		{
			if (count > 0) {
				var iter = self.GetEnumerator();
				while (count-- > 1) {
					iter.MoveNext();
					yield return iter.Current;
					yield return separator();
				}

				iter.MoveNext();
				yield return iter.Current;
			}
		}

		public static IEnumerable<U> Map<T, U>(this IEnumerable<T> self, Func<T, U> f)
		{
			return self.Select(f);
		}

		public static T? Nth<T>(this IEnumerable<T> self, int n, T? _ = null)
			where T : class?
		{
			int i = 0;
			foreach (var elem in self) {
				if (i++ == n) {
					return elem;
				}
			}

			return null;
		}

		public static T? Nth<T>(this IEnumerable<T> self, int n, T? _ = null)
			where T : struct
		{
			int i = 0;
			foreach (var elem in self) {
				if (i++ == n) {
					return elem;
				}
			}

			return null;
		}

		public static (List<T>, List<T>) Partition<T>(this IEnumerable<T> self, Func<T, bool> f)
		{
			var l = new List<T>();
			var r = new List<T>();

			foreach (var elem in self) {
				if (f(elem)) {
					l.Add(elem);
				} else {
					r.Add(elem);
				}
			}

			return (l, r);
		}

		public static int Position<T>(this IEnumerable<T> self, Func<T, bool> predicate)
		{
			int i = 0;
			foreach (var elem in self) {
				if (predicate(elem)) {
					return i;
				} else {
					++i;
				}
			}

			return -1;
		}

		public static T? Reduce<T>(this IEnumerable<T> self, Func<T, T, T> f, T? _ = null)
			where T : class
		{
			var iter = self.GetEnumerator();
			if (iter.MoveNext()) {
				var acc = iter.Current;
				while (iter.MoveNext()) {
					acc = f(acc, iter.Current);
				}

				return acc;
			} else {
				return null;
			}
		}

		public static T? Reduce<T>(this IEnumerable<T> self, Func<T, T, T> f, T? _ = null)
			where T : struct
		{
			var iter = self.GetEnumerator();
			if (iter.MoveNext()) {
				var acc = iter.Current;
				while (iter.MoveNext()) {
					acc = f(acc, iter.Current);
				}

				return acc;
			} else {
				return null;
			}
		}

		public static IEnumerable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(this IEnumerable<TFirst> self, IEnumerable<TSecond> other)
		{
			return self.Zip(other, (first, second) => (first, second));
		}
	}

	internal static class IReadOnlyCollectionExt
	{
		public static bool IsEmptyRO<T>(this IReadOnlyCollection<T> self) => self.Count == 0;
	}

	internal static class Iter
	{
		public static IEnumerable<T> Empty<T>()
		{
			yield break;
		}

#pragma warning disable IDE0001 // Simplify Names
		public static Nullable<T> Nullable<T>(T value)
			where T : struct
		{
			return new Nullable<T>(value);
		}

#pragma warning restore IDE0001

		public static IEnumerable<T> Once<T>(T value)
		{
			yield return value;
		}

		public static IEnumerable<T> Repeat<T>(T elt)
		{
			while (true) {
				yield return elt;
			}
		}
	}

	internal static class StringExt
	{
		public static bool IsEmpty(this string self) => self.Length == 0;
	}

	internal sealed class Cycle<T> : IEnumerable<T>
	{
		private readonly IEnumerable<T> _iter;

		public Cycle(IEnumerable<T> iter) => this._iter = iter;

		public IEnumerator<T> GetEnumerator() => new Impl(this._iter);

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		private sealed class Impl : IEnumerator<T>
		{
			private readonly IEnumerable<T> _source;

			private IEnumerator<T> _iter;

			private bool? _some = null;

			public Impl(IEnumerable<T> source)
			{
				this._source = source;
				this._iter = source.GetEnumerator();
			}

			public T Current => this._iter.Current;

			object IEnumerator.Current => ((IEnumerator)this._iter).Current;

			public void Dispose()
			{
				this._iter.Dispose();
				GC.SuppressFinalize(this);
			}

			public bool MoveNext()
			{
				if (!this._some.HasValue) {
					this._some = this._iter.MoveNext();
					return this._some.Value;
				} else if (this._some.Value) {
					if (!this._iter.MoveNext()) {
						this._iter = this._source.GetEnumerator();
						this._iter.MoveNext();
					}
					return true;
				} else {
					return false;
				}
			}

			public void Reset() => this._iter = this._source.GetEnumerator();
		}
	}
}
