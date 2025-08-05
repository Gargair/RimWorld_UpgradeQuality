using RimWorld;
using Verse;

namespace UpgradeQuality
{
    public struct ThingDefCountQuality
    {
        public ThingDef ThingDef { get; set; }
        public int Count { get; set; }
        public QualityRange Range { get; set; }

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
