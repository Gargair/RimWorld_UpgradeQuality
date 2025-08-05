using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UpgradeQuality.Building
{
    public class CommandUpgradeQualityBuilding : Command_Action
    {
        public CommandUpgradeQualityBuilding()
        {
            icon = ContentFinder<Texture2D>.Get("UpgradeQuality/UI/QualityUp");
            defaultLabel = "UpgQlty.Labels.UpgradeBuilding".Translate();
            defaultDesc = "UpgQlty.Tooltips.UpgradeBuilding".Translate();
            action = () => Find.WindowStack.Add(new FloatMenu(GetFloatingOptions().ToList())
            {
                vanishIfMouseDistant = true
            });
        }

        private static void ChangeTo(QualityCategory cat, bool keepQuality)
        {
            List<object> list = Find.Selector.SelectedObjects.FindAll((object o) => o is ThingWithComps);
            foreach (object item in list)
            {
                if (item is ThingWithComps thing)
                {
                    CompUpgradeQualityBuilding upgradeQualityComp = thing.TryGetComp<CompUpgradeQualityBuilding>();
                    upgradeQualityComp?.SetDesiredQualityTo(cat, keepQuality);
                }
            }
        }

        private static IEnumerable<FloatMenuOption> GetFloatingOptions()
        {
            List<object> list = Find.Selector.SelectedObjects.FindAll((object o) => o is ThingWithComps);
            QualityCategory lowestQcFound = QualityCategory.Legendary;
            foreach (object item in list)
            {
                if (item is ThingWithComps thing && thing.TryGetQuality(out QualityCategory itemQc) && itemQc < lowestQcFound)
                {
                    lowestQcFound = itemQc;
                }
            }
            List<QualityCategory> RenderQualityCategories = QualityUtility.AllQualityCategories.ListFullCopy().Where(q => q <= UpgradeQuality.Settings.MaxQuality).ToList();
            RenderQualityCategories.Reverse();
            foreach (var cat in RenderQualityCategories)
            {
                if (cat > lowestQcFound)
                {
                    yield return new FloatMenuOption("UpgQlty.Labels.UpgradeTo".Translate(cat.GetLabel()), () => ChangeTo(cat, false));
                }
                if (UpgradeQuality.Settings.IsKeepOptionEnabled)
                {
                    yield return new FloatMenuOption("UpgQlty.Labels.UpgradeToKeep".Translate(cat.GetLabel()), () => ChangeTo(cat, true));
                }
            }
        }
    }
}
