using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
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
            var upgradeBuildingCompProps = new CompPropertiesUpgradeQualityBuilding();
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

            if (ModLister.GetActiveModWithIdentifier("MalteSchulze.RIMMSqol") != null)
            {
                LogMessage("Found active RIMMSqol trying to add compability");
                Type qolModType = Type.GetType("RIMMSqol.QOLMod, RIMMSqol");
                if (qolModType != null)
                {
                    try
                    {
                        Action<Mod> callback = (Mod mod) =>
                        {
                            UpgradeQualityUtility.ClearCachedBaseCosts();
                            LogMessage("Cleared cache after setting change");
                        };
                        MethodInfo addApplySettingsListenerMethod = qolModType.GetMethod("addApplySettingsListener", BindingFlags.Static | BindingFlags.Public);
                        addApplySettingsListenerMethod.Invoke(null, new object[] { callback });
                        LogMessage("Compability with RIMMSqol added");
                    }
                    catch (Exception ex)
                    {
                        LogError("Failed to add compability with RIMMSqol");
                        LogError(ex);
                    }
                }
                else
                {
                    LogError("Did not find type for RIMMSqol.QOLMod despite mod active.");
                }
            }
        }

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
            if (q == null)
            {
                return new List<ThingDefCountQuality>();
            }
            var mult = GetMultiplier(q.Quality);
            if (CachedBaseCosts.TryGetValue((thing.def, thing.Stuff), out List<ThingDefCountQuality> tmpCostList))
            {
                return MultiplyCosts(tmpCostList, mult);
            }
            if (thing.def is BuildableDef building)
            {
                tmpCostList = building.CostListAdjusted(thing.Stuff).Select(c => new ThingDefCountQuality(c.thingDef, c.count)).ToList();
                CachedBaseCosts[(thing.def, thing.Stuff)] = tmpCostList;
                return MultiplyCosts(tmpCostList, mult);
            }
            tmpCostList = new List<ThingDefCountQuality>();
            RecipeDef recipeDef = GetRecipeForThing(thing.def);
            if (recipeDef != null)
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
            else
            {
                tmpCostList.Add(new ThingDefCountQuality(thing.def, 1, new QualityRange(q.Quality, q.Quality)));
            }
            CachedBaseCosts[(thing.def, thing.Stuff)] = tmpCostList;

            return MultiplyCosts(tmpCostList, mult);
        }

        private static RecipeDef GetRecipeForThing(ThingDef thingDef)
        {
            IEnumerable<RecipeDef> recipes = from r in DefDatabase<RecipeDef>.AllDefsListForReading
                                             where r.products.Count == 1
                                               && r.products.Any((ThingDefCountClass p) => p.thingDef == thingDef)
                                               && !r.IsSurgery
                                               && !r.ingredients.NullOrEmpty<IngredientCount>()
                                             select r;
            return recipes.FirstOrDefault();
        }

        private static List<ThingDefCountQuality> MultiplyCosts(List<ThingDefCountQuality> costs, float multiplier)
        {
            return costs.Select(x => new ThingDefCountQuality(x.ThingDef, Mathf.CeilToInt(x.Count * multiplier), x.Range)).ToList();
        }

        public static void ClearCachedBaseCosts()
        {
            CachedBaseCosts.Clear();
            RecipesForThingDef.Clear();
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
            thing = thing?.GetInnerIfMinified() as ThingWithComps;
            if (thing == null || thing.Faction != Faction.OfPlayer)
            {
                return false;
            }
            if (!thing.TryGetQuality(out var quality))
            {
                return false;
            }

#if DEBUG
            if (Find.Selector.IsSelected(thing))
            {
                UpgradeQualityUtility.LogMessage("Is Player Faction with Quality");
                UpgradeQualityUtility.LogMessage("Is Quality:", quality);
                UpgradeQualityUtility.LogMessage("IsKeepOptionEnabled:", UpgradeQuality.Settings.IsKeepOptionEnabled);
            }
#endif

            if (!UpgradeQuality.Settings.IsKeepOptionEnabled && quality >= UpgradeQuality.Settings.MaxQuality)
            {
                return false;
            }

            if (thing is Verse.Building building && BuildCopyCommandUtility.FindAllowedDesignator(building.def, true) != null)
            {
                return true;
            }

            if (!RecipesForThingDef.ContainsKey(thing.def))
            {
                List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading.Where(recipe => recipe.ProducedThingDef == thing.def).ToList();
                RecipesForThingDef.Add(thing.def, recipes);
            }

            var thingRecipes = RecipesForThingDef[thing.def];

            // If there are no recipes for the thing, it can be upgraded
            if (thingRecipes.Count == 0)
            {
                return true;
            }

            return thingRecipes.Any(recipe => recipe.AvailableNow);
        }

        private static readonly Dictionary<ThingDef, List<RecipeDef>> RecipesForThingDef = new Dictionary<ThingDef, List<RecipeDef>>();
    }
}
