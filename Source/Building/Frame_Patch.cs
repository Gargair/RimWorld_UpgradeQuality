using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Verse;

namespace UpgradeQuality.Building
{
#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public class Frame_Patch_CompleteConstruction
    {
        static bool Prefix(Frame __instance, Pawn worker)
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

#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public class Frame_Patch_FailConstruction
    {
        static bool Prefix(Frame __instance, Pawn worker)
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

#if V14
    [HarmonyPatch(typeof(Frame), nameof(Frame.MaterialsNeeded))]
    public class Frame_Patch_MaterialsNeeded
    {
        static bool Prefix(Frame __instance, ref List<ThingDefCountClass> __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                __result = new List<ThingDefCountClass>();
                var neededResouces = frame.NeededResources;
                if (neededResouces != null)
                {
                    foreach (var thingDefCountClass in neededResouces)
                    {
                        int countInContainer = __instance.resourceContainer.TotalStackCountOfDef(thingDefCountClass.ThingDef);
                        int countNeeded = thingDefCountClass.Count - countInContainer;
                        if (countNeeded > 0)
                        {
                            __result.Add(new ThingDefCountClass(thingDefCountClass.ThingDef, countNeeded));
                        }
                    }
                }
                return false;
            }
            return true;
        }
    }
#endif

#if V15
#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Frame), nameof(Frame.TotalMaterialCost))]
    public class Frame_Patch_TotalMaterialCost
    {
        static bool Prefix(Frame __instance, ref List<ThingDefCountClass> __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                __result = frame.NeededResources.Select(c => new ThingDefCountClass(c.ThingDef, c.Count)).ToList();
                return false;
            }
            return true;
        }
    }
#endif

#if V14
    [HarmonyPatch(typeof(Frame), nameof(Frame.GetInspectString))]
    internal static class Frame_Patch_GetInspectString
    {

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string BaseGetInspectString(Frame instance) { return null; }

        static bool Prefix(Frame __instance, ref string __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(BaseGetInspectString(__instance));
                __result = frame.CustomGetInspectString(stringBuilder);
                return false;
            }
            return true;
        }
    }
#endif

#if V15
#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Frame), nameof(Frame.GetInspectString))]
    public class Frame_GetInspectString
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo defField = AccessTools.DeclaredField(typeof(Thing), nameof(Thing.def));
            MethodInfo costListAdjustedMethod = AccessTools.DeclaredMethod(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted), new System.Type[] { typeof(BuildableDef), typeof(ThingDef), typeof(bool) });
            MethodInfo totalMaterialCostMethod = AccessTools.DeclaredMethod(typeof(Frame), nameof(Frame.TotalMaterialCost));
            bool inReplacing = false;
            bool didReplace = false;
            bool foundTotalMaterialCostMethod = false;
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(defField))
                {
#if DEBUG && DEBUGBUILDINGS
                    UpgradeQualityUtility.LogMessage("Found start field for replace");
#endif
                    inReplacing = true;
                }
                if (!inReplacing)
                {
                    yield return instruction;
                }
#if DEBUG && DEBUGBUILDINGS
                else
                {
                    UpgradeQualityUtility.LogMessage("Skipped instruction", instruction.ToString());
                }
#endif
                if (instruction.Calls(costListAdjustedMethod))
                {
#if DEBUG && DEBUGBUILDINGS
                    UpgradeQualityUtility.LogMessage("Found end call for replace. Emitting call");
#endif
                    yield return CodeInstruction.Call(typeof(Frame), nameof(Frame.TotalMaterialCost));
                    didReplace = true;
                    inReplacing = false;
                }
                if (instruction.Calls(totalMaterialCostMethod))
                {
                    UpgradeQualityUtility.LogWarning("Found replacing method. If you see this warning please inform mod author. It is likely this can be ignored otherwise.");
                    foundTotalMaterialCostMethod = true;
                }
            }
            if (!inReplacing && !didReplace && !foundTotalMaterialCostMethod)
            {
                UpgradeQualityUtility.LogError("Transpiler for Frame.GetInspectString did not find its anchor.");
            }
            if (inReplacing)
            {
                UpgradeQualityUtility.LogError("Transpiler for Frame.GetInspectString did not find ending instruction.");
            }
        }
    }
#endif

#if PatchCategory
    [HarmonyPatchCategory("UpgradeBuildings")]
#endif
    [HarmonyPatch(typeof(Frame), nameof(Frame.WorkToBuild), MethodType.Getter)]
    internal static class Frame_WorkToBuild
    {
        public static void Postfix(Frame __instance, ref float __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                if (BuildCopyCommandUtility.FindAllowedDesignator(__instance.def.entityDefToBuild, true) == null)
                {
                    __result = __instance.def.entityDefToBuild.GetStatValueAbstract(StatDefOf.WorkToMake, __instance.Stuff);
                }
#if V14
                var qualityComp = frame.thingToChange.TryGetComp<CompQuality>();
                if(qualityComp != null) {
                    __result *= UpgradeQualityUtility.GetMultiplier(qualityComp.Quality);
                }
#else
                if (frame.thingToChange.TryGetComp<CompQuality>(out var qualityComp))
                {
                    __result *= UpgradeQualityUtility.GetMultiplier(qualityComp.Quality);
                }
#endif
            }
        }
    }
}
