using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace UpgradeQuality.Building
{
    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
    internal static class ReplaceFrameNoWipe
    {
        public static void Postfix(BuildableDef newEntDef, ref bool __result)
        {
            if (newEntDef is ThingDef newThing && FrameUtility.IsUpgradeBuildingFrame(newThing))
            {
                __result = false;
            }
        }
    }
}
