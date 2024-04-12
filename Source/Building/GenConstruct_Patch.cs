using HarmonyLib;
using RimWorld;
using Verse;

namespace UpgradeQuality.Building
{
#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(GenConstruct), "BlocksConstruction")]
    internal class ReplaceFrameNoBlock
    {
        public static bool Prefix(Thing constructible, Thing t, ref bool __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(constructible))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
