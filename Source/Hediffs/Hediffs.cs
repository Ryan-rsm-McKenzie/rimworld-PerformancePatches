using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerformancePatches.Hediffs
{
	[StaticConstructorOnStartup]
	internal static class Manager
	{
		private static readonly Dictionary<Pawn_HealthTracker, Instanced> s_trackers = new Dictionary<Pawn_HealthTracker, Instanced>();

		public static void InvalidateCache(bool force = false)
		{
			if (force) {
				s_trackers.Clear();
				return;
			}

			var existing = Utils.AllPawnsTicking()
				.Select((pawn) => pawn.health)
				.ToHashSet();
			if (existing != null) {
				var missing = s_trackers.Keys
					.Where((k) => !existing.Contains(k))
					.ToList();
				foreach (var tracker in missing) {
					s_trackers.Remove(tracker);
				}
				foreach (var tracker in s_trackers.Values) {
					tracker.Invalidate();
				}
			} else {
				s_trackers.Clear();
			}
		}

		public static void InvalidateTracker(Pawn_HealthTracker tracker)
		{
			if (s_trackers.TryGetValue(tracker, out var instance)) {
				instance.Invalidate();
			}
		}

		public static void TickTracker(Pawn_HealthTracker tracker)
		{
			if (!s_trackers.TryGetValue(tracker, out var instance)) {
				instance = new Instanced(tracker);
				s_trackers.Add(tracker, instance);
			}

			instance.Tick();
		}
	}

	internal class Instanced
	{
		private static readonly Assembly s_vanillaAssembly = typeof(Hediff).Assembly;

		private readonly HediffCache _hediffs = new HediffCache();

		private readonly Pawn _pawn;

		private readonly Pawn_HealthTracker _tracker;

		private float _bleedRate = 0;

		private bool _dirty = true;

		private int _lastRareTicked = -1;

		public Instanced(Pawn_HealthTracker tracker)
		{
			this._tracker = tracker;
			this._pawn = tracker.pawn;
		}

		internal enum TickType
		{
			Always,

			Rarely,
		}

		private IEnumerable<Hediff> AllHediffs {
			get {
				this.RecalculateIfNeeded();
				return this._hediffs.TickAlways.Concat(this._hediffs.TickRarely);
			}
		}

		private float BleedRateTotal => this._bleedRate;

		private bool Dead => this._tracker.Dead;

		private IList<Hediff> Hediffs {
			get {
				this.RecalculateIfNeeded();
				return this._hediffs.TickAlways;
			}
		}

		private bool IsFlesh => this._pawn.RaceProps.IsFlesh;

		private IList<Hediff> RareHediffs {
			get {
				this.RecalculateIfNeeded();
				return this._hediffs.TickRarely;
			}
		}

		public void Invalidate()
		{
			this._dirty = true;
		}

		public void Tick()
		{
			if (this.Dead) {
				return;
			}

			this.TickHediffs();
			this.RemoveHediffs();

			if (this.Dead) {
				return;
			}

			this.TickImmunity();
			this.TickHealing();
			this.TickBleeding();

			if (this._pawn.IsHashIntervalTick(60)) {
				if (this.TickGivers()) {
					this.TickStory();
				}
			}
		}

		private bool HasHediffsNeedingTendByPlayer()
		{
			if (this.AllHediffs.Any((x) => x.TendableNow())) {
				if (this._pawn.NonHumanlikeOrWildMan()) {
					if (this._pawn.Faction == Faction.OfPlayer) {
						return true;
					} else if (this._pawn.CurrentBed()?.Faction == Faction.OfPlayer) {
						return true;
					}
				} else if (this._pawn.HostFaction == Faction.OfPlayer || this._pawn.Faction == Faction.OfPlayer) {
					return true;
				}
			}

			return false;
		}

		private void RecalculateBleedRate()
		{
			if (!this.IsFlesh || this.Dead || this._pawn.Deathresting) {
				this._bleedRate = 0;
			} else {
				float factor = 1;
				float rate = 0;
				foreach (var hediff in this._tracker.hediffSet.hediffs) {
					factor *= hediff.CurStage?.totalBleedFactor ?? 1;
					rate += hediff.BleedRate;
				}
				this._bleedRate = rate * factor / this._pawn.HealthScale;
			}
		}

		private void RecalculateHediffs()
		{
			this._hediffs.Clear();
			var hediffs = this._tracker.hediffSet.hediffs;
			var filter = hediffs
					.ToLookup((x) => {
						if (x.GetType().Assembly != s_vanillaAssembly) {
							return TickType.Always;
						} else if (x.def.AlwaysAllowMothball || x.IsPermanent()) {
							return TickType.Rarely;
						} else if (x is Hediff_MissingPart missing) {
							return missing.Bleeding ? TickType.Always : TickType.Rarely;
						} else {
							return TickType.Always;
						}
					});
			foreach (TickType type in Enum.GetValues(typeof(TickType))) {
				this._hediffs[type].AddRange(filter[type]);
			}
		}

		private void RecalculateIfNeeded()
		{
			if (this._dirty) {
				this.RecalculateHediffs();
				this.RecalculateBleedRate();
				this._lastRareTicked = UnityEngine.Random.Range(0, this._hediffs.TickRarely.Count) - 1;
				this._dirty = false;
			}
		}

		private void RemoveHediffs()
		{
			foreach (var hediff in this.AllHediffs) {
				if (hediff.ShouldRemove) {
					this._tracker.RemoveHediff(hediff);
				}
			}
		}

		private void TickBleeding()
		{
			if (this.IsFlesh && this.BleedRateTotal >= 0.1f) {
				float bleeding = this.BleedRateTotal * this._pawn.BodySize;
				bleeding *= (this._pawn.GetPosture() == PawnPosture.Standing) ? 0.004f : 0.0004f;
				if (Rand.Value < bleeding) {
					this._tracker.DropBloodFilth();
				}
			}
		}

		private bool TickGivers()
		{
			return this._pawn
				.RaceProps
				.hediffGiverSets?
				.SelectMany((x) => x.hediffGivers)
				.All((x) => {
					x.OnIntervalPassed(this._pawn, null);
					return !this.Dead;
				})
				?? true;
		}

		private void TickHealing()
		{
			if (this.IsFlesh && this._pawn.IsHashIntervalTick(600) && !this._pawn.Starving()) {
				float healingFactor = this._pawn.GetStatValue(StatDefOf.InjuryHealingFactor) * 0.01f * this._pawn.HealthScale;
				var injuries = this.AllHediffs.SelectNotNull((x) => x as Hediff_Injury);
				bool healed = false;
				healed = this.TickNaturalHealing(healingFactor, injuries.Where((x) => x.CanHealNaturally())) || healed;
				healed = this.TickTendedHealing(healingFactor, injuries.Where((x) => x.CanHealFromTending())) || healed;
				if (healed &&
					!this.HasHediffsNeedingTendByPlayer() &&
					!HealthAIUtility.ShouldSeekMedicalRest(this._pawn) &&
					PawnUtility.ShouldSendNotificationAbout(this._pawn)) {
					Messages.Message("MessageFullyHealed".Translate(this._pawn.LabelCap, this._pawn), this._pawn, MessageTypeDefOf.PositiveEvent);
				}
			}
		}

		private bool TickHediff(Hediff hediff)
		{
			try {
				hediff.Tick();
				hediff.PostTick();
				return true;
			} catch (Exception e) {
				Log.Error($"Exception ticking hediff {hediff.ToStringSafe()} for pawn {this._pawn.ToStringSafe()}. Removing hediff... Exception: {e}");
				try {
					this._tracker.RemoveHediff(hediff);
				} catch (Exception e2) {
					Log.Error($"Error while removing hediff: {e2}");
				}
				return false;
			}
		}

		private void TickHediffs()
		{
			this.Hediffs.All(this.TickHediff);
			this.TickRareHediffs();
		}

		private void TickImmunity()
		{
			this._tracker.immunity.ImmunityHandlerTick();
		}

		private bool TickNaturalHealing(float healingFactor, IEnumerable<Hediff_Injury> injuries)
		{
			if (injuries.Any()) {
				float healing = 8;

				if (this._pawn.GetPosture() != PawnPosture.Standing) {
					healing += 4;
					healing += this._pawn.CurrentBed()?.def.building.bed_healPerDay ?? 0;
				}

				foreach (var hediff in this.AllHediffs) {
					var stage = hediff.CurStage;
					if ((stage?.naturalHealingFactor ?? -1) != -1) {
						healing *= stage.naturalHealingFactor;
					}
				}

				injuries.RandomElement().Heal(healing * healingFactor);
				return true;
			} else {
				return false;
			}
		}

		private void TickRareHediffs()
		{
			var rare = this.RareHediffs;
			if (rare.Count > 0) {
				rare.ForEach((x) => x.ageTicks += 1);
				this._lastRareTicked = (this._lastRareTicked + 1) % rare.Count;
				var hediff = rare[this._lastRareTicked];
				hediff.ageTicks -= 1;
				this.TickHediff(hediff);
			}
		}

		private void TickStory()
		{
			this._pawn
				.story?
				.traits
				.allTraits
				.Where((x) => !x.Suppressed)
				.SelectNotNull((trait) => {
					float mtb = trait.CurrentData.randomDiseaseMtbDays;
					if (mtb > 0f && Rand.MTBEventOccurs(mtb, 60000, 60)) {
						var biome = this._pawn.Tile != -1 ? Find.WorldGrid[this._pawn.Tile].biome : DefDatabase<BiomeDef>.GetRandom();
						return DefDatabase<IncidentDef>.AllDefs
							.Where((x) => x.category == IncidentCategoryDefOf.DiseaseHuman)
							.RandomElementByWeightWithFallback((x) => biome.CommonalityOfDisease(x));
					} else {
						return null;
					}
				})
				.ForEach((incident) => {
					bool applied = ((IncidentWorker_Disease)incident.Worker)
						.ApplyToPawns(Gen.YieldSingle(this._pawn), out string blockedInfo)
						.Count == 1;
					if (PawnUtility.ShouldSendNotificationAbout(this._pawn)) {
						if (applied) {
							Find.LetterStack.ReceiveLetter(
								"LetterLabelTraitDisease".Translate(incident.diseaseIncident.label),
								"LetterTraitDisease".Translate(this._pawn.LabelCap, incident.diseaseIncident.label, this._pawn.Named("PAWN"))
									.AdjustedFor(this._pawn),
								LetterDefOf.NegativeEvent,
								this._pawn);
						} else if (!blockedInfo.NullOrEmpty()) {
							Messages.Message(blockedInfo, this._pawn, MessageTypeDefOf.NeutralEvent);
						}
					}
				});
		}

		private bool TickTendedHealing(float healingFactor, IEnumerable<Hediff_Injury> injuries)
		{
			if (injuries.Any() && !this._pawn.Starving()) {
				var injury = injuries.RandomElement();
				float quality = injury.TryGetComp<HediffComp_TendDuration>().tendQuality;
				float healing = GenMath.LerpDouble(0f, 1f, 0.5f, 1.5f, Mathf.Clamp01(quality));
				injury.Heal(8f * healing * healingFactor);
				return true;
			} else {
				return false;
			}
		}

		internal class HediffCache
		{
			public readonly List<Hediff> TickAlways = new List<Hediff>();

			public readonly List<Hediff> TickRarely = new List<Hediff>();

			public List<Hediff> this[TickType key] => key == TickType.Rarely ? this.TickRarely : this.TickAlways;

			public void Clear()
			{
				this.TickAlways.Clear();
				this.TickRarely.Clear();
			}
		}
	}
}
