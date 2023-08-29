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
	[HarmonyPatch("AddPrecept")]
	[HarmonyPatch(new Type[] { typeof(Precept), typeof(bool), typeof(FactionDef), typeof(RitualPatternDef) })]
	internal class Ideo_AddPrecept
	{
		public static void Postfix(Precept precept)
		{
			Manager.InvalidatePrecept(precept, Insertion.Added);
		}
	}

	[HarmonyPatch(typeof(Ideo))]
	[HarmonyPatch("IdeoTick")]
	[HarmonyPatch(new Type[] { })]
	internal class Ideo_IdeoTick
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var previous = new CodeMatch[] {
				new CodeMatch(OpCodes.Ldc_I4_0),
				new CodeMatch(OpCodes.Stloc_0),
				new CodeMatch(OpCodes.Br_S),
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ideo), "precepts")),
				new CodeMatch(OpCodes.Ldloc_0),
				new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<Precept>), "get_Item", new Type[] { typeof(int) })),
				new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Precept), "Tick", new Type[] { })),
				new CodeMatch(OpCodes.Ldloc_0),
				new CodeMatch(OpCodes.Ldc_I4_1),
				new CodeMatch(OpCodes.Add),
				new CodeMatch(OpCodes.Stloc_0),
				new CodeMatch(OpCodes.Ldloc_0),
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Ideo), "precepts")),
				new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<Precept>), "get_Count", new Type[] { })),
				new CodeMatch(OpCodes.Blt_S),
			};

			var matcher = new CodeMatcher(instructions);
			matcher.MatchStartForward(previous);
			if (matcher.IsValid) {
				matcher.RemoveInstructions(previous.Length);
				instructions = matcher.Instructions();
			}

			return instructions;
		}
	}

	[HarmonyPatch(typeof(Ideo))]
	[HarmonyPatch("RemovePrecept")]
	[HarmonyPatch(new Type[] { typeof(Precept), typeof(bool) })]
	internal class Ideo_RemovePrecept
	{
		public static void Postfix(Precept precept)
		{
			Manager.InvalidatePrecept(precept, Insertion.Removed);
		}
	}

	[HarmonyPatch(typeof(IdeoManager))]
	[HarmonyPatch("Add")]
	[HarmonyPatch(new Type[] { typeof(Ideo) })]
	internal class IdeoManager_Add
	{
		public static void Postfix(Ideo ideo)
		{
			Manager.InvalidateIdeo(ideo, Insertion.Removed);
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
		public static void Postfix(Ideo ideo)
		{
			Manager.InvalidateIdeo(ideo, Insertion.Removed);
		}
	}

	[HarmonyPatch(typeof(MapPawns))]
	[HarmonyPatch("DeRegisterPawn")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class MapPawns_DeRegisterPawn
	{
		public static void Postfix(Pawn p)
		{
			Manager.InvalidatePawn(p, Insertion.Removed);
		}
	}

	[HarmonyPatch(typeof(MapPawns))]
	[HarmonyPatch("RegisterPawn")]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	internal class MapPawns_RegisterPawn
	{
		public static void Postfix(Pawn p)
		{
			Manager.InvalidatePawn(p, Insertion.Added);
		}
	}

	[HarmonyPatch(typeof(Precept_Ritual))]
	[HarmonyPatch("AddObligation")]
	[HarmonyPatch(new Type[] { typeof(RitualObligation) })]
	internal class Precept_Ritual_AddObligation
	{
		public static void Postfix(RitualObligation obligation)
		{
			Manager.InvalidateObligation(obligation, Insertion.Added);
		}
	}

	[HarmonyPatch(typeof(Precept_Ritual))]
	[HarmonyPatch("RemoveObligation")]
	[HarmonyPatch(new Type[] { typeof(RitualObligation), typeof(bool) })]
	internal class Precept_Ritual_RemoveObligation
	{
		public static void Postfix(RitualObligation obligation)
		{
			Manager.InvalidateObligation(obligation, Insertion.Removed);
		}
	}
}
