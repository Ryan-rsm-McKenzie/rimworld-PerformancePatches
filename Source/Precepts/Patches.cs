#pragma warning disable IDE1006 // Naming Styles

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerformancePatches.Precepts
{
	[HarmonyPatch(typeof(Ideo))]
	[HarmonyPatch("IdeoTick")]
	[HarmonyPatch(new Type[] { })]
	internal class Ideo_IdeoTick
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _, ILGenerator generator)
		{
			var ret = generator.DefineLabel();
			return new CodeInstruction[] {
				new CodeInstruction(OpCodes.Ldarg_0),
				CodeInstruction.LoadField(typeof(Ideo), "colonistBelieverCountCached"),
				new CodeInstruction(OpCodes.Ldc_I4_M1),
				new CodeInstruction(OpCodes.Bne_Un_S, ret),
				new CodeInstruction(OpCodes.Ldarg_0),
				CodeInstruction.Call(typeof(Ideo), "RecacheColonistBelieverCount", new Type[] { }),
				new CodeInstruction(OpCodes.Pop),
				new CodeInstruction(OpCodes.Ret) { labels = new List<Label>(){ ret } },
			};
		}
	}

	[HarmonyPatch(typeof(IdeoManager))]
	[HarmonyPatch("Add")]
	[HarmonyPatch(new Type[] { typeof(Ideo) })]
	internal class IdeoManager_Add
	{
		public static void Postfix()
		{
			Manager.InvalidateCache();
		}
	}

	[HarmonyPatch(typeof(IdeoManager))]
	[HarmonyPatch("IdeoManagerTick")]
	[HarmonyPatch(new Type[] { })]
	internal class IdeoManager_IdeoManagerTick
	{
		public static void Postfix()
		{
			Manager.Tick();
		}
	}

	[HarmonyPatch(typeof(IdeoManager))]
	[HarmonyPatch("Remove")]
	[HarmonyPatch(new Type[] { typeof(Ideo) })]
	internal class IdeoManager_Remove
	{
		public static void Postfix()
		{
			Manager.InvalidateCache();
		}
	}

	[HarmonyPatch(typeof(MapPawns))]
	[HarmonyPatch("DeRegisterPawn")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class MapPawns_DeRegisterPawn
	{
		public static void Postfix()
		{
			Manager.InvalidateCache();
		}
	}

	[HarmonyPatch(typeof(MapPawns))]
	[HarmonyPatch("RegisterPawn")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class MapPawns_RegisterPawn
	{
		public static void Postfix()
		{
			Manager.InvalidateCache();
		}
	}
}
