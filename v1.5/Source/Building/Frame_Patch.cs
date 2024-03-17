using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace UpgradeQuality.Building
{
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public class Frame_Patch_CompleteConstruction
    {
        static bool Prefix(Frame __instance, Pawn worker)
        {
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "CompleteConstruction Prefix");
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found custom Frame");
                frame.CustomCompleteConstruction(worker);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public class Frame_Patch_FailConstruction
    {
        static bool Prefix(Frame __instance, Pawn worker)
        {
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "FailConstruction Prefix");
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found custom Frame");
                frame.CustomFailConstruction(worker);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.TotalMaterialCost))]
    public class Frame_Patch_TotalMaterialCost
    {
        static bool Prefix(Frame __instance, ref List<ThingDefCountClass> __result)
        {
            if (FrameUtility.IsUpgradeBuildingFrame(__instance, out var frame))
            {
                __result = new List<ThingDefCountClass>();
                var neededResouces = frame.NeededResources;
                if (neededResouces != null)
                {
                    __result = neededResouces;
                }
                return false;
            }
            return true;
        }
    }

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
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found start field for replace");
                    inReplacing = true;
                }
                if (!inReplacing)
                {
                    yield return instruction;
                }
                else
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Skipped instruction", instruction.ToString());
                }
                if (instruction.Calls(costListAdjustedMethod))
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found end call for replace. Emitting call");
                    yield return CodeInstruction.Call(typeof(Frame), nameof(Frame.TotalMaterialCost));
                    didReplace = true;
                    inReplacing = false;
                }
                if (instruction.Calls(totalMaterialCostMethod))
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found replacing method");
                    foundTotalMaterialCostMethod = true;
                }
            }
            if (!didReplace && !foundTotalMaterialCostMethod)
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Error, "Transpiler for Frame.GetInspectString did not find its anchor.");
            }
            if (inReplacing)
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Error, "Transpiler for Frame.GetInspectString did not find ending instruction.");
            }
        }
    }

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

    [HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
    internal static class ReplaceFrameNoWipe
    {
        public static void Postfix(BuildableDef newEntDef, BuildableDef oldEntDef, ref bool __result)
        {
            var newThing = newEntDef as ThingDef;
            if (newThing != null && FrameUtility.IsUpgradeBuildingFrame(newThing))
            {
                __result = false;
                return;
            }
        }
    }
}
