using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UpgradeQuality.Items;
using Verse;
using Verse.AI.Group;

namespace UpgradeQuality.Building
{
    public class Frame_UpgradeQuality_Building : Frame
    {
        public ThingWithComps thingToChange;
        public QualityCategory generatedForQuality;

        public QualityCategory? DesiredQuality
        {
            get
            {
                return Comp?.desiredQuality;
            }
        }
        public List<ThingDefCountQuality> NeededResources;
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

#if V14
        public string CustomGetInspectString(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLineIfNotEmpty();
            stringBuilder.AppendLine("ContainedResources".Translate() + ":");
            List<ThingDefCountQuality> list = NeededResources;
            for (int i = 0; i < list.Count; i++)
            {
                ThingDefCountQuality need = list[i];
                int num = need.Count;
                IEnumerable<ThingDefCountClass> source = this.MaterialsNeeded();
                foreach (ThingDefCountClass thingDefCountClass in source.Where(needed => needed.thingDef == need.ThingDef))
                {
                    num -= thingDefCountClass.count;
                }
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    need.ThingDef.LabelCap + ": ",
                    num,
                    " / ",
                    need.Count
                }));
            }
            stringBuilder.Append("WorkLeft".Translate() + ": " + this.WorkLeft.ToStringWorkAmount());
            return stringBuilder.ToString();
        }
#endif
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref thingToChange, "UpgQlty.thingToChange");
            Scribe_Values.Look(ref generatedForQuality, "UpgQlty.generatedForQuality", QualityCategory.Awful, true);
        }

        public void CustomCompleteConstruction(Pawn worker)
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("CustomCompleteConstruction");
#endif
            this.resourceContainer.ClearAndDestroyContents(DestroyMode.Vanish);

            var qualityComp = thingToChange.GetComp<CompQuality>();
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality.HasValue ? KeepQuality.Value : false;
            var comp = Comp;

            if (qualityComp != null && qualityComp.Quality < desiredQuality)
            {
                qualityComp.SetQuality(qualityComp.Quality + 1, ArtGenerationContext.Colony);
            }
#if V14
            var compArt = thingToChange.TryGetComp<CompArt>();
            if (compArt != null)
            {
                compArt.JustCreatedBy(worker);
            }
#else
            if(thingToChange.TryGetComp<CompArt>(out CompArt compArt))
            {
                compArt.JustCreatedBy(worker);
            }
#endif

            if (!this.Destroyed)
            {
                this.Destroy(DestroyMode.Vanish);
            }
            // The destroy implicitly cancels the upgrade.
            comp.SetDesiredQualityTo(desiredQuality, keepQuality);

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

        public void CustomFailConstruction(Pawn worker)
        {
            Map map = base.Map;
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality.HasValue ? KeepQuality.Value : false;
            var comp = Comp;
            this.Destroy(DestroyMode.FailConstruction);
            // The destroy implicitly cancels the upgrade.
            comp.SetDesiredQualityTo(desiredQuality, keepQuality);
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
