using HarmonyLib;
using Mono.Unix.Native;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UpgradeQuality.Building;
using UpgradeQuality.Items;
using Verse;

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
#if DEBUG
            LogMessage("Finished adding comps to thingDefs");
#endif
            var harmony = new Harmony("rakros.rimworld.upgradequality");
            FrameUtility.AddCustomFrames();
#if PatchCategory
            try
            {
                harmony.PatchCategory("UpgradeItems");
            }
            catch (Exception e)
            {
                LogError(e);
                LogError("Upgrading of items will not work.");
            }
            try
            {
                harmony.PatchCategory("UpgradeBuildings");
            }
            catch (Exception e)
            {
                LogError(e);
                LogError("Upgrading of buildings will not work.");
            }
#else
            harmony.PatchAll();
            var innerDisplayClass = AccessTools.FirstInner(typeof(Verse.AI.Toils_Haul), (inner) => inner.Name.Contains("<>c__DisplayClass6_0"));
            if (innerDisplayClass == null)
            {
                LogError("Failed to find type for patching of Toils_Haul");
                return;
            }
            var method = AccessTools.FirstMethod(innerDisplayClass, (m) => m.Name.Contains("<PlaceHauledThingInCell>b__0"));
            if (method == null)
            {
                LogError("Failed to find method for patching of Toils_Haul");
                return;
            }
            var transpiler = AccessTools.Method(typeof(Toils_Haul_Patch_PlacedThings), nameof(Toils_Haul_Patch_PlacedThings.Transpiler));
            harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
#endif
        }

        public static UpgradeQualitySettings Settings;

        public static void LogWarning(params object[] messages)
        {
            var actualMessage = messages.Aggregate("[UpgradeQuality]", (logMessage, message) => logMessage + " " + message.ToStringSafe());
            Log.Warning(actualMessage);
        }

        public static void LogError(params object[] messages)
        {
            var actualMessage = messages.Aggregate("[UpgradeQuality]", (logMessage, message) => logMessage + " " + message.ToStringSafe());
            Log.Error(actualMessage);
        }

        public static void LogMessage(params object[] messages)
        {
            var actualMessage = messages.Aggregate("[UpgradeQuality]", (logMessage, message) => logMessage + " " + message.ToStringSafe());
            Log.Message(actualMessage);
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
                    LogError("Requested multiplier for legendary quality. What?");
                    return 0;
                default:
                    LogError($"What the hell is {fromQuality} quality?");
                    return 0;
            }
        }

        public static bool CanBeUpgraded(ThingWithComps thing)
        {
            thing = thing.GetInnerIfMinified() as ThingWithComps;
            if (thing.Faction == Faction.OfPlayer && thing.TryGetQuality(out var quality))
            {
#if DEBUG
                if (Find.Selector.IsSelected(thing))
                {
                    UpgradeQualityUtility.LogMessage("Is Player Faction with Quality");
                    UpgradeQualityUtility.LogMessage("Is Quality:", quality);
                    UpgradeQualityUtility.LogMessage("IsKeepOptionEnabled:", UpgradeQuality.Settings.IsKeepOptionEnabled);
                }
#endif
                if (UpgradeQuality.Settings.IsKeepOptionEnabled || quality < QualityCategory.Legendary)
                {
                    if (thing is Verse.Building building)
                    {
#if DEBUG
                        if (Find.Selector.IsSelected(thing))
                        {
                            UpgradeQualityUtility.LogMessage("Is building");
                        }
#endif
                        if (BuildCopyCommandUtility.FindAllowedDesignator(building.def, true) != null)
                        {
#if DEBUG
                            if (Find.Selector.IsSelected(thing))
                            {
                                UpgradeQualityUtility.LogMessage("Is building with allowed designator");
                            }
#endif
                            return true;
                        }

                    }
                    else
                    {
#if DEBUG
                        if (Find.Selector.IsSelected(thing))
                        {
                            UpgradeQualityUtility.LogMessage("No building");
                        }
#endif
                    }

                    if (!RecipesForThingDef.ContainsKey(thing.def))
                    {
#if DEBUG
                        if (Find.Selector.IsSelected(thing))
                        {
                            UpgradeQualityUtility.LogMessage("building recipe cache");
                        }
#endif
                        List<RecipeDef> recipes = new List<RecipeDef>();
                        List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
                        for (int j = 0; j < allRecipes.Count; j++)
                        {
                            if (allRecipes[j].ProducedThingDef == thing.def)
                            {
                                recipes.Add(allRecipes[j]);
                            }
                        }
                        RecipesForThingDef.Add(thing.def, recipes);
                    }

                    var thingRecipes = RecipesForThingDef[thing.def];

#if DEBUG
                    if (Find.Selector.IsSelected(thing) && thingRecipes.Count == 0)
                    {
                        UpgradeQualityUtility.LogMessage("No recipes");
                    }
#endif
                    foreach (var recipe in thingRecipes)
                    {
                        if (recipe.AvailableNow)
                        {
#if DEBUG
                            if (Find.Selector.IsSelected(thing))
                            {
                                UpgradeQualityUtility.LogMessage("Has recipe available now");
                            }
#endif
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static readonly Dictionary<ThingDef, List<RecipeDef>> RecipesForThingDef = new Dictionary<ThingDef, List<RecipeDef>>();
    }
}
