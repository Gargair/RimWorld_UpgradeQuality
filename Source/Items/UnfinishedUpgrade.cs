using Verse;

namespace UpgradeQuality.Items
{
    public class UnfinishedUpgrade : UnfinishedThing
    {
        private Thing _thingToUpgrade;
        public Thing ThingToUpgrade { get => this._thingToUpgrade; set => this._thingToUpgrade = value; }

        public override string DescriptionFlavor => ThingToUpgrade.DescriptionFlavor;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _thingToUpgrade, "thingToUpgrade");
        }

        public override string LabelNoCount
        {
            get
            {
                return "UnfinishedItem".Translate(this.ThingToUpgrade.LabelNoCount);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.Cancel && this.ThingToUpgrade != null)
            {
                GenPlace.TryPlaceThing(this.ThingToUpgrade, base.Position, base.Map, ThingPlaceMode.Near);
            }

            base.Destroy(mode);
        }
    }
}
