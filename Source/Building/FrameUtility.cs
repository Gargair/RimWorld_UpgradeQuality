using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace UpgradeQuality.Building
{
    internal class FrameUtility
    {
        private static readonly Dictionary<ThingDef, ThingDef> frameCache = new Dictionary<ThingDef, ThingDef>();

        public static ThingDef GetFrameDefForThingDef(ThingDef def)
        {
            if (frameCache.ContainsKey(def)) return frameCache[def];
            UpgradeQualityUtility.LogError($"Missing frame def for {def.defName} in framecache. Wrong load order?");
            Type typeFromHandle = typeof(ThingDef);
            HashSet<ushort> h = ((Dictionary<Type, HashSet<ushort>>)AccessTools.Field(typeof(ShortHashGiver), "takenHashesPerDeftype").GetValue(null))[typeFromHandle];
            var frameDef = NewReplaceFrameDef_Thing(def);
            GiveShortHash(frameDef, typeFromHandle, h);
            frameDef.PostLoad();
            DefDatabase<ThingDef>.Add(frameDef);
            frameCache.Add(def, frameDef);
            return frameDef;
        }

        public static bool IsUpgradeBuildingFrame(ThingDef def)
        {
            return def != null && frameCache.ContainsValue(def);
        }

        public static bool IsUpgradeBuildingFrame(Thing thing)
        {
            return thing != null && IsUpgradeBuildingFrame(thing.def);
        }

        public static bool IsUpgradeBuildingFrame(Thing thing, out FrameUpgradeQualityBuilding frame)
        {
            if (IsUpgradeBuildingFrame(thing))
            {
                frame = (FrameUpgradeQualityBuilding)thing;
                return true;
            }
            frame = null;
            return false;
        }

        public static void AddCustomFrames()
        {
            Type typeFromHandle = typeof(ThingDef);
            HashSet<ushort> h = ((Dictionary<Type, HashSet<ushort>>)AccessTools.Field(typeof(ShortHashGiver), "takenHashesPerDeftype").GetValue(null))[typeFromHandle];
            foreach (ThingDef upgradeBuildingThingDef in DefDatabase<ThingDef>.AllDefs.Where(td => td.HasComp(typeof(CompUpgradeQualityBuilding))))
            {
                ThingDef upgradeFrameDef = NewReplaceFrameDef_Thing(upgradeBuildingThingDef);
                frameCache[upgradeBuildingThingDef] = upgradeFrameDef;
                GiveShortHash(upgradeFrameDef, typeFromHandle, h);
                upgradeFrameDef.PostLoad();
                DefDatabase<ThingDef>.Add(upgradeFrameDef);
            }
        }

        private static ThingDef NewReplaceFrameDef_Thing(ThingDef def)
        {
            ThingDef thingDef = BaseFrameDef();
            thingDef.defName = def.defName + "_UpgradeBuildingQuality";
            thingDef.label = def.label;
            thingDef.size = def.size;
            thingDef.SetStatBaseValue(StatDefOf.MaxHitPoints, (float)def.BaseMaxHitPoints * 0.25f);
            thingDef.SetStatBaseValue(StatDefOf.Beauty, -8f);
            thingDef.SetStatBaseValue(StatDefOf.Flammability, def.BaseFlammability);
            thingDef.fillPercent = 0.2f;
            thingDef.pathCost = 14;
            thingDef.description = def.description;
            thingDef.passability = def.passability;
            if (thingDef.passability > Traversability.PassThroughOnly)
            {
                thingDef.passability = Traversability.PassThroughOnly;
            }
            thingDef.selectable = def.selectable;
            thingDef.constructEffect = def.constructEffect;
            if (def.building != null)
            {
                thingDef.building.isEdifice = false;
                thingDef.building.watchBuildingInSameRoom = def.building.watchBuildingInSameRoom;
                thingDef.building.watchBuildingStandDistanceRange = def.building.watchBuildingStandDistanceRange;
                thingDef.building.watchBuildingStandRectWidth = def.building.watchBuildingStandRectWidth;
                thingDef.building.artificialForMeditationPurposes = def.building.artificialForMeditationPurposes;
            }
            else
            {
                thingDef.building = null;
            }
            thingDef.constructionSkillPrerequisite = def.constructionSkillPrerequisite;
            thingDef.artisticSkillPrerequisite = def.artisticSkillPrerequisite;
            thingDef.clearBuildingArea = false;
            thingDef.drawPlaceWorkersWhileSelected = def.drawPlaceWorkersWhileSelected;
            thingDef.stuffCategories = def.stuffCategories;
            thingDef.entityDefToBuild = def;
            thingDef.modContentPack = LoadedModManager.GetMod<Mod>().Content;
            return thingDef;
        }

        private static ThingDef BaseFrameDef()
        {
            return new ThingDef
            {
                isFrameInt = true,
                category = ThingCategory.Building,
                label = "Unspecified building upgrade quality frame",
                thingClass = typeof(FrameUpgradeQualityBuilding),
                altitudeLayer = AltitudeLayer.BuildingOnTop,
                useHitPoints = true,
                selectable = true,
                building = new BuildingProperties(),
                comps =
                {
                    new CompProperties_Forbiddable()
                },
                scatterableOnMapGen = false,
                leaveResourcesWhenKilled = true
            };
        }

        private static readonly GiveShortHashDel GiveShortHash = AccessTools.MethodDelegate<GiveShortHashDel>(AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash", null, null), null, true);
        private delegate void GiveShortHashDel(Def d, Type t, HashSet<ushort> h);
    }
}
