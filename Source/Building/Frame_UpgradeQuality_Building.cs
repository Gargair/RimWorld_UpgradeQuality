using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI.Group;
using Verse.Sound;
using Verse;

namespace UpgradeQuality.Building
{
    internal class Frame_UpgradeQuality_Building : Frame
    {
        public ThingWithComps thingToChange;
        public QualityCategory? DesiredQuality
        {
            get
            {
                return Comp?.desiredQuality;
            }
        }
        public List<ThingDefCountClass> NeededResources
        {
            get
            {
                return Comp?.neededResources;
            }
        }
        public bool? KeepQuality
        {
            get
            {
                return Comp?.keepQuality;
            }
        }


        private Comp_UpgradeQuality_Building Comp => thingToChange?.GetComp<Comp_UpgradeQuality_Building>();

        public override void Notify_KilledLeavingsLeft(List<Thing> leavings)
        {
            base.Notify_KilledLeavingsLeft(leavings);
            Comp?.CancelUpgrade();
        }

        public string CustomGetInspectString(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLineIfNotEmpty();
            stringBuilder.AppendLine("ContainedResources".Translate() + ":");
            List<ThingDefCountClass> list = this.CustomCostListAdjusted();
            for (int i = 0; i < list.Count; i++)
            {
                ThingDefCountClass need = list[i];
                int num = need.count;
                IEnumerable<ThingDefCountClass> source = this.MaterialsNeeded();
                foreach (ThingDefCountClass thingDefCountClass in source.Where(needed => needed.thingDef == need.thingDef))
                {
                    num -= thingDefCountClass.count;
                }
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    need.thingDef.LabelCap + ": ",
                    num,
                    " / ",
                    need.count
                }));
            }
            stringBuilder.Append("WorkLeft".Translate() + ": " + this.WorkLeft.ToStringWorkAmount());
            return stringBuilder.ToString();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref thingToChange, "UpgQlty.thingToChange");
        }

        public void CustomCompleteConstruction(Pawn worker)
        {
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "CustomCompleteConstruction");

            this.resourceContainer.ClearAndDestroyContents(DestroyMode.Vanish);

            var qualityComp = thingToChange.GetComp<CompQuality>();
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality.HasValue ? KeepQuality.Value : false;
            var comp = Comp;

            if (qualityComp != null && qualityComp.Quality < desiredQuality)
            {
                qualityComp.SetQuality(qualityComp.Quality + 1, ArtGenerationContext.Colony);
            }

            if (!this.Destroyed)
            {
                this.Destroy(DestroyMode.Vanish);
            }
            // The destroy implicitly cancels the upgrade.

            if (qualityComp.Quality >= desiredQuality)
            {
                // TODO: Send Notification
                if (keepQuality)
                {
                    comp.SetDesiredQualityTo(desiredQuality, keepQuality);
                }
            }
            else
            {
                comp.SetDesiredQualityTo(desiredQuality, keepQuality);
            }

            worker.records.Increment(RecordDefOf.ThingsConstructed);
            if (thingToChange != null && thingToChange.GetStatValue(StatDefOf.WorkToBuild, true, -1) >= 9500f)
            {
                TaleRecorder.RecordTale(TaleDefOf.CompletedLongConstructionProject, new object[]
                {
                    worker,
                    thingToChange.def
                });
            }
        }

        public List<ThingDefCountClass> CustomCostListAdjusted()
        {
            return NeededResources;
        }
    }
}
