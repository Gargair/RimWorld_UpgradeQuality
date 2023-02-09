using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace UpgradeQuality.Building
{
    public class CompProperties_UpgradeQuality_Building : CompProperties
    {
        public TickerType originalTickerType;
        public CompProperties_UpgradeQuality_Building(TickerType tickerType)
        {
            compClass = typeof(Comp_UpgradeQuality_Building);
            this.originalTickerType = tickerType;
        }
    }
}