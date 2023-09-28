#pragma warning disable IDE1006 // Naming Styles
#nullable enable

using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerformancePatches.Bugfixes
{
	[HarmonyPatch(typeof(QuestPart_Bestowing_TargetChangedTitle))]
	[HarmonyPatch("Notify_QuestSignalReceived")]
	[HarmonyPatch(new Type[] { typeof(Signal) })]
	internal class QuestPart_Bestowing_TargetChangedTitle_Notify_QuestSignalReceived
	{
		public static bool Prefix(QuestPart_Bestowing_TargetChangedTitle __instance)
		{
			if ((__instance.pawn?.Dead ?? true) || (__instance.bestower?.Dead ?? true)) {
				__instance.quest?.End(QuestEndOutcome.Unknown, false);
				return false;
			} else {
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(RestUtility))]
	[HarmonyPatch("IsCharging")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class RestUtility_IsCharging
	{
		public static bool Prefix(ref bool __result, Pawn p)
		{
			if (p.needs is null) {
				__result = false;
				return false;
			} else {
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(RestUtility))]
	[HarmonyPatch("IsSelfShutdown")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class RestUtility_IsSelfShutdown
	{
		public static bool Prefix(ref bool __result, Pawn p)
		{
			if (p.needs is null) {
				__result = false;
				return false;
			} else {
				return true;
			}
		}
	}
}
