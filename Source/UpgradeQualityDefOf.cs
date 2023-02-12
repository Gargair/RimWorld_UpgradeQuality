using RimWorld;
using Verse;

namespace UpgradeQuality
{
    public static class UpgradeQualityDefOf
    {
        [DefOf]
        public static class Designations
        {
            public static DesignationDef IncreaseQuality_Building;
            public static DesignationDef IncreaseQuality_Items;
        }

        [DefOf]
        public static class Jobs
        {
            public static JobDef IncreaseQuality_Job;
        }
    }
}
