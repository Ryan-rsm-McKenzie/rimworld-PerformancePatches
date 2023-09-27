#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Iterator;
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
		private static readonly DirtyCache s_dirty = new();

		private static readonly Dictionary<Ideo, HashSet<Pawn>> s_ideos = new();

		private static readonly ObligationCache s_obligations = new();

		private static readonly List<Precept> s_precepts = new();

		public static void InvalidateCache(bool force = false)
		{
			if (force) {
				s_ideos.Clear();
				s_precepts.Clear();
				s_obligations.Clear();
			}

			s_dirty.Ideos = true;
			s_dirty.Precepts = true;
			s_dirty.Obligations = true;
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
						.FilterMap((map) => map.mapPawns?.AllPawns)
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

		public static void InvalidateObligation(RitualObligation obligation, Insertion _)
		{
			if (s_ideos.ContainsKey(obligation.precept.ideo)) {
				s_dirty.Obligations = true;
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
			RecalculateIfNeeded();
			TickPrecepts();
			TickObligations();
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
			RecalculateObligationsIfNeeded();
		}

		private static void RecalculateObligationsIfNeeded()
		{
			if (s_dirty.Obligations) {
				s_obligations.Clear();

				var t = typeof(Precept_Ritual);
				var rituals = s_ideos.Keys
					.SelectMany((ideo) => ideo.PreceptsListForReading)
					.Where((precept) => precept.GetType() == t) // don't include custom types that derive from Precept_Ritual
					.Distinct()
					.Cast<Precept_Ritual>();

				var obligations = rituals
					.FilterMap((ritual) => ritual.activeObligations)
					.Flatten();
				var triggers = rituals
					.FilterMap((ritual) => ritual.obligationTriggers)
					.Flatten();

				s_obligations.Active.AddRange(obligations);
				s_obligations.Triggers.AddRange(triggers);

				s_dirty.Obligations = false;
			}
		}

		private static void RecalculatePreceptsIfNeeded()
		{
			if (s_dirty.Precepts) {
				var vanilla = typeof(Ideo).Assembly;
				var role = typeof(Precept_Role);

				s_precepts.Clear();
				var precepts = s_ideos.Keys
					.SelectMany((ideo) => ideo.PreceptsListForReading)
					.Distinct()
					.Where((precept) => {
						var type = precept.GetType();
						return type.Assembly != vanilla || type == role;
					});
				s_precepts.AddRange(precepts);

				s_dirty.Precepts = false;
				s_dirty.Obligations = true;
			}
		}

		private static void TickObligations()
		{
			foreach (var obligation in s_obligations.Active) {
				var precept = obligation.precept;
				if (!obligation.StillValid || !precept.obligationTargetFilter.ObligationTargetsValid(obligation)) {
					precept.RemoveObligation(obligation);
				}
			}

			foreach (var trigger in s_obligations.Triggers) {
				try {
					trigger.Tick();
				} catch (Exception e) {
					Log.Error($"Error while ticking a ritual obligation trigger: {e}");
				}
			}
		}

		private static void TickPrecepts()
		{
			foreach (var precept in s_precepts) {
				precept.Tick();
			}
		}

		private class DirtyCache
		{
			public bool Ideos = true;

			public bool Obligations = true;

			public bool Precepts = true;
		}

		private class ObligationCache
		{
			public readonly List<RitualObligation> Active = new();

			public readonly List<RitualObligationTrigger> Triggers = new();

			public void Clear()
			{
				this.Active.Clear();
				this.Triggers.Clear();
			}
		}
	}
}
