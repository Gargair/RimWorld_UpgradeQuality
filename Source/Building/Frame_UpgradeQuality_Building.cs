using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI.Group;

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
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    need.thingDef.LabelCap + ": ",
                    this.resourceContainer.TotalStackCountOfDef(need.thingDef),
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

        public void CustomFailConstruction(Pawn worker)
        {
            Map map = base.Map;
            var qualityComp = thingToChange.GetComp<CompQuality>();
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality.HasValue ? KeepQuality.Value : false;
            var comp = Comp;
            this.Destroy(DestroyMode.FailConstruction);
            // The destroy implicitly cancels the upgrade.
            if (qualityComp.Quality >= desiredQuality)
            {
                if (keepQuality)
                {
                    comp.SetDesiredQualityTo(desiredQuality, keepQuality);
                }
            }
            else
            {
                comp.SetDesiredQualityTo(desiredQuality, keepQuality);
            }
            Lord lord = worker.GetLord();
            if (lord != null)
            {
                lord.Notify_ConstructionFailed(worker, this, null);
            }
            MoteMaker.ThrowText(this.DrawPos, map, "TextMote_ConstructionFail".Translate(), 6f);
            if (base.Faction == Faction.OfPlayer && this.WorkToBuild > 1400f)
            {
                Messages.Message("MessageConstructionFailed".Translate(this.LabelEntityToBuild, worker.LabelShort, worker.Named("WORKER")), new TargetInfo(base.Position, map, false), MessageTypeDefOf.NegativeEvent, true);
            }
        }
    }
}
