using HarmonyLib;
using Verse;

namespace UpgradeQuality.Building
{
#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Designation), "Notify_Removing")]
    public class Designation_Notify_Removing
    {
        public static void Postfix(Designation __instance)
        {
            if (__instance.def == UpgradeQualityDefOf.IncreaseQuality_Building && __instance.target.HasThing)
            {
#if V14
                if (__instance.target.Thing is ThingWithComps thingWithComps)
                {
                    var upgComp = thingWithComps.TryGetComp<Comp_UpgradeQuality_Building>();
                    if (upgComp != null)
                    {
                        upgComp.SkipRemoveDesignation = true;
                        upgComp.CancelUpgrade();
                        upgComp.SkipRemoveDesignation = false;
                    }
                }
#else
                if (__instance.target.Thing is ThingWithComps thingWithComps && thingWithComps.TryGetComp<Comp_UpgradeQuality_Building>(out var upgComp))
                {
                    upgComp.SkipRemoveDesignation = true;
                    upgComp.CancelUpgrade();
                    upgComp.SkipRemoveDesignation = false;
                }
#endif
            }
        }
    }
}
