﻿using HarmonyLib;
using Verse;

namespace PerformancePatches
{
	public class ModMain : Mod
	{
		private readonly Harmony _harmony;

		public ModMain(ModContentPack content)
			: base(content)
		{
			this._harmony = new Harmony(this.Content.PackageIdPlayerFacing);
			this._harmony.PatchAll();
			this.GetSettings<Settings>();
		}

		private class Settings : ModSettings
		{
			public override void ExposeData()
			{
				if (Scribe.mode == LoadSaveMode.LoadingVars) {
					Hediffs.Manager.InvalidateCache();
				}
			}
		}
	}
}
