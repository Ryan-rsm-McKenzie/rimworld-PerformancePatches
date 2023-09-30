#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Extensions;
using Iterator;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerformancePatches.Hediffs
{
	[StaticConstructorOnStartup]
	internal static class Manager
	{
		private static readonly Dictionary<Type, bool> s_cachedCompsToSkip = new();

		private static readonly Dictionary<Type, bool> s_cachedHediffsToSkip = new();

		private static readonly ConditionalWeakTable<Pawn_HealthTracker, Instanced> s_trackers = new();

		static Manager()
		{
			DefDatabase<HediffDef>.AllDefsListForReading
				.FilterMap(x => x.comps)
				.Flatten()
				.FilterMap(x => x.compClass)
				.Distinct()
				.ForEach(x => CacheComp(x));
			DefDatabase<HediffDef>.AllDefsListForReading
				.FilterMap(x => x.hediffClass)
				.Distinct()
				.ForEach(x => CacheHediff(x));
		}

		public static bool CanSkipComp(HediffComp comp)
		{
			var type = comp.GetType();
			if (!s_cachedCompsToSkip.TryGetValue(type, out bool skip)) {
				skip = CacheComp(type);
			}

			return skip;
		}

		public static bool CanSkipHediff(Hediff hediff)
		{
			var type = hediff.GetType();
			if (!s_cachedHediffsToSkip.TryGetValue(type, out bool skip)) {
				skip = CacheHediff(type);
			}

			return skip;
		}

		public static string DebugStringFor(Pawn_HealthTracker tracker, Hediff hediff)
		{
			if (s_trackers.TryGetValue(tracker, out var instance)) {
				return instance.DebugStringFor(hediff);
			} else {
				return string.Empty;
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

		private static bool CacheComp(Type comp)
		{
			bool skip = comp
				.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.Filter(x =>
					x.IsVirtual &&
					x.GetParameters().Length == 0 &&
					x.Name switch {
						"CompPostTick" => true,
						_ => false,
					})
				.All(x => {
					const byte RET = 0x2A;
					byte[] il = x.GetMethodBody().GetILAsByteArray();
					return il.Length == 1 && il[0] == RET;
				});
			s_cachedCompsToSkip[comp] = skip;
			return skip;
		}

		private static bool CacheHediff(Type hediff)
		{
			bool skip = hediff
				.GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.Filter(x => x.IsVirtual && x.GetParameters().Length == 0)
				.All(x => {
					var methods = x.Name switch {
						"Tick" => new MethodInfo[] { typeof(Hediff).GetMethod("Tick", new Type[] { }) },
						"PostTick" => new MethodInfo[] {
							typeof(Hediff).GetMethod("PostTick", new Type[] { }),
							typeof(HediffWithComps).GetMethod("PostTick", new Type[] { }),
						},
						_ => new MethodInfo[] { },
					};

					if (methods.IsEmpty()) {
						return true;
					} else {
						byte[] il = x.GetMethodBody().GetILAsByteArray();
						return methods.Any(x => x.GetMethodBody().GetILAsByteArray().SequenceEqual(il));
					}
				});

			s_cachedHediffsToSkip[hediff] = skip;
			return skip;
		}
	}

	internal class Instanced
	{
		private readonly Utils.WeakReference<Pawn_HealthTracker> _tracker;

		private List<Hediff> _always = new();

		private bool _dirty = true;

		private List<Hediff> _rarely = new();

		public Instanced(Pawn_HealthTracker tracker) => this._tracker = new(tracker);

		private IReadOnlyList<Hediff> AllHediffs => this.Tracker.hediffSet.hediffs;

		private float BleedRateTotal => this.Tracker.hediffSet.BleedRateTotal;

		private bool Dead => this.Tracker.Dead;

		private bool IsFlesh => this.Pawn.RaceProps.IsFlesh;

		private Pawn Pawn => this.Tracker.pawn;

		private Pawn_HealthTracker Tracker => this._tracker.Target!;

		public string DebugStringFor(Hediff hediff)
		{
			string prefix = this._rarely.Contains(hediff) ? "Suppressed" : "Actively";
			return $"--{prefix} ticking";
		}

		public void Invalidate() => this._dirty = true;

		public void Tick()
		{
			if (this.Dead) {
				return;
			}

			this.RecalculateIfNeeded();

			this.TickHediffs();
			this.RemoveHediffs();

			if (this.Dead) {
				return;
			}

			this.TickImmunity();
			this.TickHealing();
			this.TickBleeding();

			if (this.Pawn.IsHashIntervalTick(60)) {
				if (this.TickGivers()) {
					this.TickStory();
				}
			}
		}

		private bool HasHediffsNeedingTendByPlayer()
		{
			if (this.AllHediffs.Any(x => x.TendableNow())) {
				var pawn = this.Pawn;
				if (pawn.NonHumanlikeOrWildMan()) {
					if (pawn.Faction == Faction.OfPlayer) {
						return true;
					} else if (pawn.CurrentBed()?.Faction == Faction.OfPlayer) {
						return true;
					}
				} else if ((pawn.Faction == Faction.OfPlayer && pawn.HostFaction == null) || pawn.HostFaction == Faction.OfPlayer) {
					return true;
				}
			}

			return false;
		}

		private void RecalculateHediffs()
		{
			(this._rarely, this._always) = this.AllHediffs
				.Partition(x => {
					if (x is Hediff_MissingPart missing) {
						return !missing.Bleeding && !missing.IsFreshNonSolidExtremity;
					} else if ((x.def.stages?.IsEmpty() ?? true) && (x.def.hediffGivers?.IsEmpty() ?? true)) {
						return Manager.CanSkipHediff(x) && ((x as HediffWithComps)?.comps.All(Manager.CanSkipComp) ?? true);
					} else {
						return false;
					}
				});
		}

		private void RecalculateIfNeeded()
		{
			if (this._dirty) {
				this.RecalculateHediffs();
				this._dirty = false;
			}
		}

		private void RemoveHediffs()
		{
			foreach (var hediff in this.AllHediffs.Clone()) {
				if (hediff.ShouldRemove) {
					this.Tracker.RemoveHediff(hediff);
				}
			}
		}

		private void TickBleeding()
		{
			if (this.IsFlesh && this.BleedRateTotal >= 1e-1f) {
				float bleeding = this.BleedRateTotal * this.Pawn.BodySize;
				bleeding *= (this.Pawn.GetPosture() == PawnPosture.Standing) ? 4e-3f : 4e-4f;
				if (Rand.Value < bleeding) {
					this.Tracker.DropBloodFilth();
				}
			}
		}

		private bool TickGivers()
		{
			return this.Pawn
				.RaceProps
				.hediffGiverSets?
				.Map(x => x.hediffGivers)
				.Flatten()
				.All(x => {
					x.OnIntervalPassed(this.Pawn, null);
					return !this.Dead;
				})
				?? true;
		}

		private void TickHealing()
		{
			var pawn = this.Pawn;
			if (this.IsFlesh && pawn.IsHashIntervalTick(600) && !pawn.Starving()) {
				float healingFactor = pawn.GetStatValue(StatDefOf.InjuryHealingFactor) * 0.01f * pawn.HealthScale;
				var injuries = this.AllHediffs.FilterMap(x => x as Hediff_Injury);
				bool healed = false;
				healed = this.TickNaturalHealing(
					healingFactor,
					injuries.Filter(x => x.CanHealNaturally()).ToList()) || healed;
				healed = this.TickTendedHealing(
					healingFactor,
					injuries.Filter(x => x.CanHealFromTending() && x.Severity > 0).ToList()) || healed;
				if (healed &&
					!this.HasHediffsNeedingTendByPlayer() &&
					!HealthAIUtility.ShouldSeekMedicalRest(pawn) &&
					PawnUtility.ShouldSendNotificationAbout(pawn)) {
					Messages.Message("MessageFullyHealed".Translate(pawn.LabelCap, pawn), pawn, MessageTypeDefOf.PositiveEvent);
				}
			}
		}

		private void TickHediffs()
		{
			foreach (var hediff in this._always) {
				try {
					hediff.Tick();
					hediff.PostTick();
				} catch (Exception inner) {
					Log.Error($"Exception ticking hediff {hediff.ToStringSafe()} for pawn {this.Pawn.ToStringSafe()}. Removing hediff... Exception: {inner}");
					try {
						this.Tracker.RemoveHediff(hediff);
					} catch (Exception outer) {
						Log.Error($"Error while removing hediff: {outer}");
					}
				}
			}

			foreach (var rare in this._rarely) {
				rare.ageTicks += 1;
			}
		}

		private void TickImmunity() => this.Tracker.immunity.ImmunityHandlerTick();

		private bool TickNaturalHealing(float healingFactor, IReadOnlyCollection<Hediff_Injury> injuries)
		{
			if (!injuries.IsEmptyRO()) {
				float healing = 8;

				var pawn = this.Pawn;
				if (pawn.GetPosture() != PawnPosture.Standing) {
					healing += 4;
					healing += pawn.CurrentBed()?.def.building.bed_healPerDay ?? 0;
				}

				foreach (var hediff in this.AllHediffs) {
					var stage = hediff.CurStage;
					if (stage is not null && stage.naturalHealingFactor != -1) {
						healing *= stage.naturalHealingFactor;
					}
				}

				injuries.Choice()!.Heal(healing * healingFactor);
				return true;
			} else {
				return false;
			}
		}

		private void TickStory()
		{
			var pawn = this.Pawn;
			pawn
				.story?
				.traits
				.allTraits
				.Filter(x => !x.Suppressed)
				.FilterMap(trait => {
					float mtb = trait.CurrentData.randomDiseaseMtbDays;
					if (mtb > 0f && Rand.MTBEventOccurs(mtb, GenDate.TicksPerDay, 60)) {
						var biome = pawn.Tile != -1 ? Find.WorldGrid[pawn.Tile].biome : DefDatabase<BiomeDef>.AllDefsListForReading.Choice()!;
						return DefDatabase<IncidentDef>.AllDefs
							.Filter(x => x.category == IncidentCategoryDefOf.DiseaseHuman)
							.RandomElementByWeightWithFallback(x => biome.CommonalityOfDisease(x));
					} else {
						return null;
					}
				})
				.ForEach(incident => {
					bool applied = ((IncidentWorker_Disease)incident.Worker)
						.ApplyToPawns(Iter.Once(pawn), out string blockedInfo)
						.Count == 1;
					if (PawnUtility.ShouldSendNotificationAbout(pawn)) {
						if (applied) {
							Find.LetterStack.ReceiveLetter(
								"LetterLabelTraitDisease".Translate(incident.diseaseIncident.label),
								"LetterTraitDisease".Translate(pawn.LabelCap, incident.diseaseIncident.label, pawn.Named("PAWN"))
									.AdjustedFor(pawn),
								LetterDefOf.NegativeEvent,
								pawn);
						} else if (!blockedInfo.IsEmpty()) {
							Messages.Message(blockedInfo, pawn, MessageTypeDefOf.NeutralEvent);
						}
					}
				});
		}

		private bool TickTendedHealing(float healingFactor, IReadOnlyCollection<Hediff_Injury> injuries)
		{
			if (!injuries.IsEmptyRO() && !this.Pawn.Starving()) {
				var injury = injuries.Choice()!;
				float quality = injury.TryGetComp<HediffComp_TendDuration>().tendQuality;
				float healing = GenMath.LerpDouble(0f, 1f, 0.5f, 1.5f, Mathf.Clamp01(quality));
				injury.Heal(8f * healing * healingFactor);
				return true;
			} else {
				return false;
			}
		}
	}
}
