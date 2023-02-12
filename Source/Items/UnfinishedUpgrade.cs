using Verse;

namespace UpgradeQuality.Items
{
    public class UnfinishedUpgrade : UnfinishedThing
    {
        public Thing thingToUpgrade;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref thingToUpgrade, "thingToUpgrade");
        }

        public override string LabelNoCount
        {
            get
            {
                return "UnfinishedItem".Translate(this.thingToUpgrade.LabelNoCount);
            }
        }
    }
}
