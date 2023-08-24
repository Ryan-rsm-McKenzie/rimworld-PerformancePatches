using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PerformancePatches
{
	[StaticConstructorOnStartup]
	internal static class Utils
	{
		private static readonly List<Pawn> s_pawns = new List<Pawn>();

		private static int s_lastUpdate = -1;

		public static IEnumerable<Pawn> AllPawnsTicking()
		{
			if (s_lastUpdate != Find.TickManager.TicksGame) {
				s_pawns.Clear();
				var maps = Find.Maps?
					.SelectNotNull((map) => map.mapPawns?.AllPawns)
					.Flatten()
					?? Enumerable.Empty<Pawn>();
				var caravans = Find.WorldObjects?
					.Caravans
					.SelectMany((caravan) => caravan.PawnsListForReading)
					?? Enumerable.Empty<Pawn>();
				var pods = Find.WorldObjects?
					.TravelingTransportPods
					.SelectMany((pod) => pod.Pawns)
					?? Enumerable.Empty<Pawn>();
				s_pawns.AddRange(maps.Concat(caravans).Concat(pods).Distinct());

				s_lastUpdate = Find.TickManager.TicksGame;
			}

			return s_pawns;
		}
	}
}
