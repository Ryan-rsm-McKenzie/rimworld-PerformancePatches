using HarmonyLib;
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
		}
	}
}
