#pragma warning disable IDE1006 // Naming Styles

using System;
using HarmonyLib;
using Verse;

namespace PerformancePatches.Hediffs
{
	[HarmonyPatch(typeof(Pawn_HealthTracker))]
	[HarmonyPatch("AddHediff")]
	[HarmonyPatch(new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo), typeof(DamageWorker.DamageResult) })]
	internal class Pawn_HealthTracker_AddHediff
	{
		public static void Postfix(Pawn_HealthTracker __instance)
		{
			GlobalHealthTracker.InvalidateTracker(__instance);
		}
	}

	[HarmonyPatch(typeof(Pawn_HealthTracker))]
	[HarmonyPatch("HealthTick")]
	[HarmonyPatch(new Type[] { })]
	internal class Pawn_HealthTracker_HealthTick
	{
		public static bool Prefix(Pawn_HealthTracker __instance)
		{
			GlobalHealthTracker.TickTracker(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Pawn_HealthTracker))]
	[HarmonyPatch("RemoveHediff")]
	[HarmonyPatch(new Type[] { typeof(Hediff) })]
	internal class Pawn_HealthTracker_RemoveHediff
	{
		public static void Postfix(Pawn_HealthTracker __instance)
		{
			GlobalHealthTracker.InvalidateTracker(__instance);
		}
	}
}
