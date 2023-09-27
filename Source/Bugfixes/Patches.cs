#pragma warning disable IDE1006 // Naming Styles
#nullable enable

using System;
using HarmonyLib;
using RimWorld;

namespace PerformancePatches.Precepts
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
}
