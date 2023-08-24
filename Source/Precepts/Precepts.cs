using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PerformancePatches.Precepts
{
	[StaticConstructorOnStartup]
	internal static class Manager
	{
		private static bool s_dirty = true;

		private static List<Precept> s_precepts = new List<Precept>();

		private static List<Precept> Precepts {
			get {
				RecalculateIfNeeded();
				return s_precepts;
			}
		}

		public static void InvalidateCache()
		{
			s_dirty = true;
		}

		public static void Tick()
		{
			foreach (var precept in Precepts) {
				precept.Tick();
			}
		}

		private static void RecalculateIfNeeded()
		{
			if (s_dirty) {
				var vanilla = typeof(Ideo).Assembly;
				var ritual = typeof(Precept_Ritual);
				var role = typeof(Precept_Role);

				s_precepts.Clear();
				var precepts = Utils.AllPawnsTicking()
					.SelectNotNull((pawn) => pawn.Ideo)
					.Distinct()
					.SelectMany((ideo) => ideo.PreceptsListForReading)
					.Distinct()
					.Where((precept) => {
						var type = precept.GetType();
						return type.Assembly != vanilla || type == ritual || type == role;
					});
				s_precepts.AddRange(precepts);

				s_dirty = false;
			}
		}
	}
}
