using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace UpgradeQuality.Building
{
    public class Command_UpgradeQuality_Building : Command_Action
    {
        static Command_UpgradeQuality_Building()
        {
            RenderQualityCategories = QualityUtility.AllQualityCategories.ListFullCopy();
            RenderQualityCategories.Reverse();
        }

        public Command_UpgradeQuality_Building()
        {
            icon = ContentFinder<Texture2D>.Get("UI/QualityUp");
            defaultLabel = "UpgQlty.Labels.UpgradeBuilding".Translate();
            defaultDesc = "UpgQlty.Tooltips.UpgradeBuilding".Translate();
            action = () => Find.WindowStack.Add(new FloatMenu(GetFloatingOptions().ToList())
            {
                vanishIfMouseDistant = true
            });
        }

        private static void ChangeTo(QualityCategory cat, bool keepQuality)
        {
            List<object> list = Find.Selector.SelectedObjects.FindAll((object o) => typeof(ThingWithComps).IsAssignableFrom(o.GetType()));
            foreach (object item in list)
            {
                if (item is ThingWithComps thing)
                {
                    Comp_UpgradeQuality_Building upgradeQualityComp = thing.TryGetComp<Comp_UpgradeQuality_Building>();
                    if (upgradeQualityComp != null)
                    {
                        upgradeQualityComp.SetDesiredQualityTo(cat, keepQuality);
                    }
                }
            }
        }

        private static IEnumerable<FloatMenuOption> GetFloatingOptions()
        {
            //var allSelectedThings = Find.Selector.SelectedObjects.FindAll((object o) => typeof(ThingWithComps).IsAssignableFrom(o.GetType())).Cast<ThingWithComps>();

            foreach (var cat in RenderQualityCategories)
            {
                yield return new FloatMenuOption("UpgQlty.Labels.UpgradeTo".Translate(cat.GetLabel()), () => ChangeTo(cat, false));
                if (UpgradeQuality.Settings.IsKeepOptionEnabled)
                {
                    yield return new FloatMenuOption("UpgQlty.Labels.UpgradeToKeep".Translate(cat.GetLabel()), () => ChangeTo(cat, true));
                }
            }
        }

        private static readonly List<QualityCategory> RenderQualityCategories;
    }
}
