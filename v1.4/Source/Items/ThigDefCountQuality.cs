using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
