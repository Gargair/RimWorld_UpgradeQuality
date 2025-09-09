using HarmonyLib;
using Verse;

namespace UpgradeQuality.Building
{
    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Designation), "Notify_Removing")]
    public static class DesignationNotifyRemoving
    {
        public static void Postfix(Designation __instance)
        {
            if (__instance.def == UpgradeQualityDefOf.IncreaseQuality_Building
             && __instance.target.HasThing
             && __instance.target.Thing is ThingWithComps thingWithComps
             && thingWithComps.TryGetComp<Comp_UpgradeQuality_Building>(out var upgComp))
            {
                upgComp.SkipRemoveDesignation = true;
                upgComp.CancelUpgrade();
                upgComp.SkipRemoveDesignation = false;
            }
        }
    }
}
