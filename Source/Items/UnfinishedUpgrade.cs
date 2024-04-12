using Verse;

namespace UpgradeQuality.Items
{
    public class UnfinishedUpgrade : UnfinishedThing
    {
        public Thing thingToUpgrade;

        public override string DescriptionFlavor => thingToUpgrade.DescriptionFlavor;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref thingToUpgrade, "thingToUpgrade");
        }

        public override string LabelNoCount
        {
            get
            {
                return "UnfinishedItem".Translate(this.thingToUpgrade.LabelNoCount);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.Cancel)
            {
                if (this.thingToUpgrade != null)
                {
                    GenPlace.TryPlaceThing(this.thingToUpgrade, base.Position, base.Map, ThingPlaceMode.Near);
                }
            }
            base.Destroy(mode);
        }
    }
}
