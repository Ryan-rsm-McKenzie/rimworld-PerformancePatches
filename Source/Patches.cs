using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerformancePatches
{
	[HarmonyPatch(typeof(TickManager))]
	[HarmonyPatch("DoSingleTick")]
	[HarmonyPatch(new Type[] { })]
	internal class TickManager_DoSingleTick
	{
		public static void Postfix()
		{
			if ((Find.TickManager.TicksGame % GenDate.TicksPerDay) == 0) {
				Hediffs.Manager.InvalidateCache();
				Precepts.Manager.InvalidateCache();
			}
		}
	}
}
