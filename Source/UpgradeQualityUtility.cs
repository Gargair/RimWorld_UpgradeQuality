using HarmonyLib;
using RimWorld;
using System;
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
            FrameUtility.AddCustomFrames();
#if PatchCategory
            try
            {
                harmony.PatchCategory("UpgradeItems");
            }
            catch (Exception e)
            {
                LogMessage(LogLevel.Error, e.ToString());
                LogMessage(LogLevel.Error, "Upgrading of items will not work.");
            }
            try
            {
                harmony.PatchCategory("UpgradeBuildings");
            }
            catch (Exception e)
            {
                LogMessage(LogLevel.Error, e.ToString());
                LogMessage(LogLevel.Error, "Upgrading of buildings will not work.");
            }
#else
            harmony.PatchAll();
            var innerDisplayClass = AccessTools.FirstInner(typeof(Toils_Haul), (inner) => inner.Name.Contains("<>c__DisplayClass6_0"));
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
#endif
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

        private static readonly Dictionary<ValueTuple<ThingDef, ThingDef>, List<ThingDefCountQuality>> CachedBaseCosts = new Dictionary<(ThingDef, ThingDef), List<ThingDefCountQuality>>();

        public static List<ThingDefCountQuality> GetNeededResources(Thing thing)
        {
            var q = thing.TryGetComp<CompQuality>();
            if (q != null)
            {
                var mult = GetMultiplier(q.Quality);
                List<ThingDefCountQuality> tmpCostList = null;
                if (CachedBaseCosts.TryGetValue((thing.def, thing.Stuff), out tmpCostList))
                {
                }
                else if (thing.def is BuildableDef building)
                {
                    tmpCostList = thing.def.CostListAdjusted(thing.Stuff).Select(c => new ThingDefCountQuality(c.thingDef, c.count)).ToList();
                    CachedBaseCosts[(thing.def, thing.Stuff)] = tmpCostList;
                }
                else
                {
                    IEnumerable<RecipeDef> recipes = from r in DefDatabase<RecipeDef>.AllDefsListForReading
                                                     where r.products.Count == 1 && r.products.Any((ThingDefCountClass p) => p.thingDef == thing.def) && !r.IsSurgery
                                                     select r;
                    tmpCostList = new List<ThingDefCountQuality>();
                    if (recipes.Any<RecipeDef>())
                    {
                        RecipeDef recipeDef = recipes.FirstOrDefault<RecipeDef>();
                        if (recipeDef != null && !recipeDef.ingredients.NullOrEmpty<IngredientCount>())
                        {
                            for (int i = 0; i < recipeDef.ingredients.Count; i++)
                            {
                                IngredientCount ingredientCount = recipeDef.ingredients[i];
                                ThingDef ingDef = null;
                                if (ingredientCount.IsFixedIngredient)
                                {
                                    ingDef = ingredientCount.FixedIngredient;
                                }
                                else
                                {
                                    ingDef = thing.Stuff;
                                }
                                tmpCostList.Add(new ThingDefCountQuality(ingDef, ingredientCount.CountRequiredOfFor(ingDef, recipeDef)));
                            }
                        }
                    }
                    else
                    {
                        tmpCostList.Add(new ThingDefCountQuality(thing.def, 1, new QualityRange(q.Quality, q.Quality)));
                    }
                    CachedBaseCosts[(thing.def, thing.Stuff)] = tmpCostList;
                }
                return tmpCostList.Select(x => new ThingDefCountQuality(x.ThingDef, Mathf.CeilToInt(x.Count * mult), x.Range)).ToList();
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
                    LogMessage(LogLevel.Error, "Requested multiplier for legendary quality. What?");
                    return 0;
                default:
                    LogMessage(LogLevel.Error, $"What the hell is {fromQuality} quality?");
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
