using RimWorld;
using Verse;

namespace UpgradeQuality.Items
{
    public struct ThingDefCountQuality
    {
        public ThingDef ThingDef;
        public int Count;
        public QualityRange Range;

        public ThingDefCountQuality(ThingDef def, int count, QualityRange qRange)
        {
            ThingDef = def;
            Count = count;
            Range = qRange;
        }

        public ThingDefCountQuality(ThingDef def, int count)
        {
            ThingDef = def;
            Count = count;
            Range = QualityRange.All;
        }
    }
}
