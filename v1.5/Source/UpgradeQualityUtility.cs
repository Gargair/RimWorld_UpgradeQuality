using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UpgradeQuality.Building;
using UpgradeQuality.Items;
using Verse;
using Verse.AI;

namespace UpgradeQuality
{
    [StaticConstructorOnStartup]
    public static class UpgradeQualityUtility
    {
        static UpgradeQualityUtility()
        {
            var upgradeBuildingCompProps = new CompProperties_UpgradeQuality_Building();
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs.Where(thingDef => thingDef.HasComp(typeof(CompQuality))))
            {
                if (thingDef.building != null || thingDef.Minifiable)
                {
                    thingDef.comps.Add(upgradeBuildingCompProps);
                }
            }

            LogMessage(LogLevel.Debug, "Finished adding comps to thingDefs");
            var harmony = new Harmony("rakros.rimworld.upgradequality");
            harmony.PatchAll();
            FrameUtility.AddCustomFrames();
            // Needs the delegate from the Toils_Haul.PlaceHauledThingInCell Method to inject our job in the check for UpdateJobWithPlacedThings action
            var innerDisplayClass = AccessTools.FirstInner(typeof(Toils_Haul), (inner) => inner.Name.Contains("<>c__DisplayClass8_0"));
            if (innerDisplayClass == null)
            {
                LogMessage(LogLevel.Error, "Failed to find type for patching of Toils_Haul");
                return;
            }
            var method = AccessTools.FirstMethod(innerDisplayClass, (m) => m.Name.Contains("<PlaceHauledThingInCell>b__0"));
            if (method == null)
            {
                LogMessage(LogLevel.Error, "Failed to find method for patching of Toils_Haul");
                return;
            }
            var transpiler = AccessTools.Method(typeof(Toils_Haul_Patch_PlacedThings), nameof(Toils_Haul_Patch_PlacedThings.Transpiler));
            harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
        }

        public static UpgradeQualitySettings Settings;

        public static LogLevel logLevel = LogLevel.Information;

        public static void LogMessage(LogLevel logLevel, params string[] messages)
        {
            var actualMessage = messages.Aggregate("[UpgradeQuality]", (logMessage, message) => logMessage + " " + message);
            if (logLevel > UpgradeQualityUtility.logLevel)
            {
                return;
            }
            switch (logLevel)
            {
                case LogLevel.Error:
                    Log.Error(actualMessage);
                    break;
                case LogLevel.Warning:
                    Log.Warning(actualMessage);
                    break;
                default:
                    Log.Message(actualMessage);
                    break;
            }
        }

        public static List<ThingDefCountClass> GetNeededResources(Thing thing)
        {
            var q = thing.TryGetComp<CompQuality>();
            if (q != null)
            {
                var l = new List<ThingDefCountClass>();
                var origCostList = thing.CostListAdjusted();
                var mult = GetMultiplier(q.Quality);
                return origCostList.Select(x => new ThingDefCountClass(x.thingDef, Mathf.CeilToInt(x.count * mult))).ToList();
            }
            return null;
        }

        public static float GetMultiplier(QualityCategory fromQuality)
        {
            switch (fromQuality)
            {
                case QualityCategory.Awful:
                    return UpgradeQuality.Settings.Factor_Awful_Poor;
                case QualityCategory.Poor:
                    return UpgradeQuality.Settings.Factor_Poor_Normal;
                case QualityCategory.Normal:
                    return UpgradeQuality.Settings.Factor_Normal_Good;
                case QualityCategory.Good:
                    return UpgradeQuality.Settings.Factor_Good_Excellent;
                case QualityCategory.Excellent:
                    return UpgradeQuality.Settings.Factor_Excellent_Masterwork;
                case QualityCategory.Masterwork:
                    return UpgradeQuality.Settings.Factor_Masterwork_Legendary;
                case QualityCategory.Legendary:
                    return 0;
                default:
                    return 0;
            }
        }
    }

    public enum LogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Information = 3,
        Debug = 4,
    }
}
