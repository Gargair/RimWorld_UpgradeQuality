using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse.AI;

namespace UpgradeQuality.Items
{
    [HarmonyPatchCategory("UpgradeItems")]
    [HarmonyPatch]
    public static class ToilsHaulPatchPlacedThings
    {
        [HarmonyTargetMethod]
        public static MethodInfo GetMethod()
        {
#if DEBUG && DEBUGITEMS
            UpgradeQualityUtility.LogMessage("Start finding method to transpile");
#endif
            foreach (var t in AccessTools.InnerTypes(typeof(Toils_Haul)))
            {
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage(t.FullName);
#endif
                MethodInfo method = AccessTools.GetDeclaredMethods(t).FirstOrDefault(m =>
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage($"\t{m.Name}");
#endif
                    return m.Name.Contains("PlaceHauledThingInCell");
                });
                if (method != null)
                {
                    return method;
                }
            }
            return null;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var holdingType = original.GetMethodBody().LocalVariables[0].LocalType;
            var matcher = new CodeMatcher(instructions);
            var get_DoBill = AccessTools.Field(typeof(JobDefOf), nameof(JobDefOf.DoBill));
            var get_CurJob = AccessTools.Field(holdingType, "curJob");
            var get_Def = AccessTools.Field(typeof(Job), nameof(Job.def));

            var toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Ldfld, get_CurJob),
                new CodeMatch(OpCodes.Ldfld, get_Def),
                new CodeMatch(OpCodes.Ldsfld, get_DoBill),
                new CodeMatch(OpCodes.Beq_S)
            };

            matcher.MatchStartForward(toMatch);

            if (matcher.IsValid)
            {
                var toCopy = matcher.InstructionsWithOffsets(0, toMatch.Length - 1);
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("CIL to Copy");
                foreach (var c in toCopy)
                {
                    UpgradeQualityUtility.LogMessage($"\t{c.opcode}\t{c.operand}");
                }
#endif
                matcher.Advance(toMatch.Length);
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Inserting copy before");
                UpgradeQualityUtility.LogMessage($"\t{matcher.Instruction.opcode}\t{matcher.Instruction.operand}");
#endif
                matcher.Insert(toCopy);
                matcher.Advance(toMatch.Length - 2);
                matcher.SetInstruction(CodeInstruction.LoadField(typeof(UpgradeQualityDefOf), nameof(UpgradeQualityDefOf.IncreaseQuality_Job)));
                return matcher.InstructionEnumeration();
            }
            else
            {
                UpgradeQualityUtility.LogError("Failed to get anchor for Toils_Haul");
                return instructions;
            }

        }
    }
}
