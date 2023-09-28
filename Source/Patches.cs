#nullable enable

using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Profile;

namespace PerformancePatches
{
	[HarmonyPatch(typeof(MemoryUtility))]
	[HarmonyPatch("ClearAllMapsAndWorld")]
	[HarmonyPatch(new Type[] { })]
	internal class MemoryUtility_ClearAllMapsAndWorld
	{
		public static void Prefix()
		{
			Precepts.Manager.InvalidateCache(true);
		}
	}

	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("DoSingleTick")]
	[HarmonyPatch(new Type[] { })]
	internal class TickManager_DoSingleTick
	{
		public static void Postfix()
		{
			if ((Find.TickManager.TicksGame % GenDate.TicksPerDay) == 0) {
				Precepts.Manager.InvalidateCache();
			}
		}
	}
}
