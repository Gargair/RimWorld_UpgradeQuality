﻿using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace UpgradeQuality.Items
{
#if V15
    [HarmonyPatchCategory("UpgradeItems")]
    [HarmonyPatch("Verse.AI.Toils_Haul+<>c__DisplayClass8_0", "<PlaceHauledThingInCell>b__0")]
#endif
    public class Toils_Haul_Patch_PlacedThings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var get_DoBill = AccessTools.Field(typeof(JobDefOf), nameof(JobDefOf.DoBill));
            var oldInstructions = instructions.ToList();
            var JobDefDoBillIndex = -1;
            for (int i = 0; i < oldInstructions.Count; i++)
            {
                var instr = oldInstructions[i];
                if (instr.LoadsField(get_DoBill))
                {
                    JobDefDoBillIndex = i;
                    break;
                }
            }
            if (JobDefDoBillIndex >= 0)
            {
                var startBatchIndex = JobDefDoBillIndex - 3;
                var endBatchIndex = JobDefDoBillIndex + 1;
                foreach (var instr in oldInstructions.GetRange(0, endBatchIndex + 1))
                {
                    yield return instr;
                }
                yield return oldInstructions[startBatchIndex].Clone();
                yield return oldInstructions[startBatchIndex + 1].Clone();
                yield return oldInstructions[startBatchIndex + 2].Clone();
                yield return CodeInstruction.LoadField(typeof(UpgradeQualityDefOf), nameof(UpgradeQualityDefOf.IncreaseQuality_Job));
                // My JobDefOf
                yield return oldInstructions[startBatchIndex + 4];
                foreach (var instr in oldInstructions.GetRange(endBatchIndex + 1, oldInstructions.Count - endBatchIndex - 1))
                {
                    yield return instr;
                }
                yield break;
            }
            else
            {
                UpgradeQualityUtility.LogError("Failed to get JobDefDoBillIndex");
                foreach (var instr in instructions)
                {
                    yield return instr;
                }
            }
        }
    }
}
