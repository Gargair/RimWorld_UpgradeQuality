using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse.AI;

namespace UpgradeQuality.Items
{
#if V15
    [HarmonyPatchCategory("UpgradeItems")]
    [HarmonyPatch]
#endif
    public class Toils_Haul_Patch_PlacedThings
    {
        [HarmonyTargetMethod]
        public static MethodInfo GetMethod()
        {
#if DEBUG && DEBUGITEMS
            UpgradeQualityUtility.LogMessage("Start finding method to transpile");
#endif
#if V15
            foreach (var t in AccessTools.InnerTypes(typeof(Toils_Haul)))
#else
            foreach(var t in typeof(Toils_Haul).GetNestedTypes(AccessTools.all))
#endif
            {
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage(t.FullName);
#endif
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage($"\t{m.Name}");
#endif
                    if (m.Name.Contains("PlaceHauledThingInCell"))
                    {
                        return m;
                    }
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
