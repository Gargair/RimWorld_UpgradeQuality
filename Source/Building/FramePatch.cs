using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Collections;
using Verse;

namespace UpgradeQuality.Building
{
    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class FramePatchCompleteConstruction
    {
        public static bool Prefix(Frame __instance, Pawn worker)
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("CompleteConstruction Prefix");
#endif
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Found custom Frame");
#endif
                frame.CustomCompleteConstruction(worker);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public static class FramePatchFailConstruction
    {
        public static bool Prefix(Frame __instance, Pawn worker)
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("FailConstruction Prefix");
#endif
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Found custom Frame");
#endif
                frame.CustomFailConstruction(worker);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Frame), nameof(Frame.TotalMaterialCost))]
    public static class FramePatchTotalMaterialCost
    {
        public static bool Prefix(Frame __instance, ref List<ThingDefCountClass> __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                __result = frame.NeededResources.Select(c => new ThingDefCountClass(c.ThingDef, c.Count)).ToList();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Frame), nameof(Frame.GetInspectString))]
    public static class FrameGetInspectString
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions);
            FieldInfo defField = AccessTools.DeclaredField(typeof(Thing), nameof(Thing.def));
            FieldInfo entityDefToBuildField = AccessTools.DeclaredField(typeof(ThingDef), nameof(ThingDef.entityDefToBuild));
            MethodInfo get_Stuff = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Stuff));
            MethodInfo costListAdjustedMethod = AccessTools.DeclaredMethod(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted), new System.Type[] { typeof(BuildableDef), typeof(ThingDef), typeof(bool) });
            MethodInfo totalMaterialCostMethod = AccessTools.DeclaredMethod(typeof(Frame), nameof(Frame.TotalMaterialCost));

            var toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, defField),
                new CodeMatch(OpCodes.Ldfld, entityDefToBuildField),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Call, get_Stuff),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Call, costListAdjustedMethod),
                new CodeMatch(OpCodes.Stloc_1)
            };

            var toReplace = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, totalMaterialCostMethod),
                new CodeInstruction(OpCodes.Stloc_1)
            };

            matcher.MatchStartForward(toMatch);

            if (matcher.IsValid)
            {
                matcher.RemoveInstructions(toMatch.Length);
                matcher.InsertAndAdvance(toReplace);
                return matcher.InstructionEnumeration();
            }
            else
            {
                UpgradeQualityUtility.LogError("Transpiler for Frame.GetInspectString did not find its anchor.");
                UpgradeQualityUtility.LogWarning("Display for frames may not show correct building materials. This is purely a UI bug.");
                return instructions;
            }
        }
    }

    [HarmonyPatchCategory("UpgradeBuildings")]
    [HarmonyPatch(typeof(Frame), nameof(Frame.WorkToBuild), MethodType.Getter)]
    public static class FrameWorkToBuild
    {
        public static void Postfix(Frame __instance, ref float __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                if (BuildCopyCommandUtility.FindAllowedDesignator(__instance.def.entityDefToBuild, true) == null)
                {
                    __result = __instance.def.entityDefToBuild.GetStatValueAbstract(StatDefOf.WorkToMake, __instance.Stuff);
                }
                if (frame.ThingToChange.TryGetComp<CompQuality>(out var qualityComp))
                {
                    __result *= UpgradeQualityUtility.GetMultiplier(qualityComp.Quality);
                }
            }
        }
    }
}
