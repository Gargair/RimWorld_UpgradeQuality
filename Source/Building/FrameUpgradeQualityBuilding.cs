using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace UpgradeQuality.Building
{
    public class FrameUpgradeQualityBuilding : Frame
    {
        private ThingWithComps _thingToChange;
        public ThingWithComps ThingToChange { get => this._thingToChange; set => this._thingToChange = value; }
        private QualityCategory _generatedForQuality;
        public QualityCategory GeneratedForQuality { get => this._generatedForQuality; set => this._generatedForQuality = value; }

        public QualityCategory? DesiredQuality
        {
            get
            {
                return this.Comp?.DesiredQuality;
            }
        }
        public List<ThingDefCountQuality> NeededResources { get; set; }
        public bool? KeepQuality
        {
            get
            {
                return this.Comp?.KeepQuality;
            }
        }

        private CompUpgradeQualityBuilding Comp => this.ThingToChange?.GetComp<CompUpgradeQualityBuilding>();

        public override void Notify_KilledLeavingsLeft(List<Thing> leavings)
        {
            base.Notify_KilledLeavingsLeft(leavings);
            Comp?.CancelUpgrade();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _thingToChange, "UpgQlty.thingToChange");
            Scribe_Values.Look(ref _generatedForQuality, "UpgQlty.generatedForQuality", QualityCategory.Awful, false);
        }

        public void CustomCompleteConstruction(Pawn worker)
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("CustomCompleteConstruction");
#endif
            this.resourceContainer.ClearAndDestroyContents(DestroyMode.Vanish);

            var qualityComp = ThingToChange.GetComp<CompQuality>();
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality ?? false;
            var comp = Comp;

            if (qualityComp != null && qualityComp.Quality < desiredQuality)
            {
                qualityComp.SetQuality(qualityComp.Quality + 1, ArtGenerationContext.Colony);
            }
            if (ThingToChange.TryGetComp<CompArt>(out CompArt compArt))
            {
                compArt.JustCreatedBy(worker);
            }

            if (!this.Destroyed)
            {
                this.Destroy(DestroyMode.Vanish);
            }
            // The destroy implicitly cancels the upgrade.
            comp?.SetDesiredQualityTo(desiredQuality, keepQuality);

            worker.records.Increment(RecordDefOf.ThingsConstructed);
            if (ThingToChange != null && ThingToChange.GetStatValue(StatDefOf.WorkToBuild, true, -1) >= 9500f)
            {
                TaleRecorder.RecordTale(TaleDefOf.CompletedLongConstructionProject, new object[]
                {
                    worker,
                    ThingToChange.def
                });
            }
        }

        public void CustomFailConstruction(Pawn worker)
        {
            Map map = base.Map;
            var desiredQuality = DesiredQuality ?? QualityCategory.Awful;
            var keepQuality = KeepQuality ?? false;
            var comp = Comp;
            this.Destroy(DestroyMode.FailConstruction);
            // The destroy implicitly cancels the upgrade.
            comp?.SetDesiredQualityTo(desiredQuality, keepQuality);
            Lord lord = worker.GetLord();
            lord?.Notify_ConstructionFailed(worker, this, null);
            MoteMaker.ThrowText(this.DrawPos, map, "TextMote_ConstructionFail".Translate(), 6f);
            if (base.Faction == Faction.OfPlayer && this.WorkToBuild > 1400f)
            {
                Messages.Message("MessageConstructionFailed".Translate(this.LabelEntityToBuild, worker.LabelShort, worker.Named("WORKER")), new TargetInfo(base.Position, map, false), MessageTypeDefOf.NegativeEvent, true);
            }
        }
    }
}
