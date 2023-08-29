using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PerformancePatches.Precepts
{
	internal enum Insertion
	{
		Added,

		Removed,
	}

	[StaticConstructorOnStartup]
	internal static class Manager
	{
		private static readonly DirtyCache s_dirty = new DirtyCache();

		private static readonly Dictionary<Ideo, HashSet<Pawn>> s_ideos = new Dictionary<Ideo, HashSet<Pawn>>();

		private static readonly List<Precept> s_precepts = new List<Precept>();

		private static List<Precept> Precepts {
			get {
				RecalculateIfNeeded();
				return s_precepts;
			}
		}

		public static void InvalidateCache(bool force = false)
		{
			if (force) {
				s_ideos.Clear();
				s_precepts.Clear();
			}

			s_dirty.Ideos = true;
			s_dirty.Precepts = true;
		}

		public static void InvalidateIdeo(Ideo ideo, Insertion insertion)
		{
			switch (insertion) {
				case Insertion.Added:
					if (!s_ideos.TryGetValue(ideo, out var followers)) {
						followers = new HashSet<Pawn>();
						s_ideos.Add(ideo, followers);
						s_dirty.Precepts = true;
					}

					followers.Clear();
					var x = Find.Maps?
						.SelectNotNull((map) => map.mapPawns?.AllPawns)
						.Flatten()
						.Where((pawn) => pawn.Ideo == ideo);
					followers.AddRange(x);

					break;
				case Insertion.Removed:
					s_ideos.Remove(ideo);
					s_dirty.Precepts = true;
					break;
			}
		}

		public static void InvalidatePawn(Pawn pawn, Insertion insertion)
		{
			var ideo = pawn.Ideo;
			if (ideo != null) {
				switch (insertion) {
					case Insertion.Added:
						if (s_ideos.ContainsKey(ideo)) {
							s_ideos[ideo].Add(pawn);
						} else {
							s_ideos.Add(ideo, new HashSet<Pawn>() { pawn });
							s_dirty.Precepts = true;
						}
						break;
					case Insertion.Removed:
						if (s_ideos.ContainsKey(ideo)) {
							var followers = s_ideos[ideo];
							followers.Remove(pawn);
							if (!followers.Any()) {
								s_ideos.Remove(ideo);
								s_dirty.Precepts = true;
							}
						}
						break;
				}
			}
		}

		public static void InvalidatePrecept(Precept precept, Insertion _)
		{
			var ideo = precept.ideo;
			if (ideo != null) {
				InvalidateIdeo(ideo, Insertion.Added);
			}
		}

		public static void Tick()
		{
			foreach (var precept in Precepts) {
				precept.Tick();
			}
		}

		private static void RecalculateIdeosIfNeeded()
		{
			if (s_dirty.Ideos) {
				s_ideos.Clear();
				Find.IdeoManager?
					.IdeosListForReading
					.ForEach((ideo) => InvalidateIdeo(ideo, Insertion.Added));
				s_dirty.Ideos = false;
				s_dirty.Precepts = true;
			}
		}

		private static void RecalculateIfNeeded()
		{
			RecalculateIdeosIfNeeded();
			RecalculatePreceptsIfNeeded();
		}

		private static void RecalculatePreceptsIfNeeded()
		{
			if (s_dirty.Precepts) {
				var vanilla = typeof(Ideo).Assembly;
				var ritual = typeof(Precept_Ritual);
				var role = typeof(Precept_Role);

				s_precepts.Clear();
				var precepts = s_ideos.Keys
					.SelectMany((ideo) => ideo.PreceptsListForReading)
					.Distinct()
					.Where((precept) => {
						var type = precept.GetType();
						return type.Assembly != vanilla || type == ritual || type == role;
					});
				s_precepts.AddRange(precepts);

				s_dirty.Precepts = false;
			}
		}

		private class DirtyCache
		{
			public bool Ideos = true;

			public bool Precepts = true;
		}
	}
}
