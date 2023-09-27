#pragma warning disable IDE1006 // Naming Styles
#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
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
			Manager.InvalidateTracker(__instance);
		}
	}

	[HarmonyPatch(typeof(Pawn_HealthTracker))]
	[HarmonyPatch("HealthTick")]
	[HarmonyPatch(new Type[] { })]
	internal class Pawn_HealthTracker_HealthTick
	{
		[HarmonyPriority(Priority.Last)]
		public static void Prefix(Pawn_HealthTracker __instance)
		{
			Manager.TickTracker(__instance);
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _)
		{
			yield return new CodeInstruction(OpCodes.Ret);
		}
	}

	[HarmonyPatch(typeof(Pawn_HealthTracker))]
	[HarmonyPatch("RemoveHediff")]
	[HarmonyPatch(new Type[] { typeof(Hediff) })]
	internal class Pawn_HealthTracker_RemoveHediff
	{
		public static void Postfix(Pawn_HealthTracker __instance)
		{
			Manager.InvalidateTracker(__instance);
		}
	}
}
