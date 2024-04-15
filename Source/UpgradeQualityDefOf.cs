using RimWorld;
using Verse;

namespace UpgradeQuality
{
    [DefOf]
    public static class UpgradeQualityDefOf
    {
        static UpgradeQualityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(UpgradeQualityDefOf));
        }

        public static DesignationDef IncreaseQuality_Building;

        public static JobDef IncreaseQuality_Job;

    }
}
