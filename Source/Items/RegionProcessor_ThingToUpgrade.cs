using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    public class RegionProcessor_ThingToUpgrade : RegionProcessorDelegateCache
    {
        private Pawn worker;
        private double searchRadius;
        private double searchRadiusSquared;
        private IntVec3 anchorCell;
        private ThingFilter itemFilter;
        private QualityCategory maxQuality;
        private bool includeMaxQuality;

        public List<Thing> ValidItems = new List<Thing>();

        public RegionProcessor_ThingToUpgrade(Pawn worker, double searchRadius, IntVec3 anchorCell, ThingFilter itemFilter, QualityCategory maxQuality, bool includeMaxQuality)
        {
            this.worker = worker;
            this.searchRadius = searchRadius;
            this.searchRadiusSquared = searchRadius * searchRadius;
            this.anchorCell = anchorCell;
            this.itemFilter = itemFilter;
            this.maxQuality = maxQuality;
            this.includeMaxQuality = includeMaxQuality;
        }

        public void Reset()
        {
            this.ValidItems.Clear();
        }

        protected override bool RegionEntryPredicate(Region from, Region to)
        {
            var traverseParams = TraverseParms.For(worker);
            if (Math.Abs(999f - searchRadius) >= 1f)
            {
                if (!to.Allows(traverseParams, false))
                {
                    return false;
                }
                CellRect extentsClose = to.extentsClose;
                int num = Math.Abs(anchorCell.x - Math.Max(extentsClose.minX, Math.Min(anchorCell.x, extentsClose.maxX)));
                if ((float)num > searchRadius)
                {
                    return false;
                }
                int num2 = Math.Abs(anchorCell.z - Math.Max(extentsClose.minZ, Math.Min(anchorCell.z, extentsClose.maxZ)));
                return (float)num2 <= searchRadius && (float)(num * num + num2 * num2) <= searchRadiusSquared;
            }
            else
            {
                return to.Allows(traverseParams, false);
            }
        }

        public void Sort()
        {
            ValidItems.Sort((Thing t1, Thing t2) => (t1.Position - anchorCell).LengthHorizontalSquared.CompareTo((t2.Position - anchorCell).LengthHorizontalSquared));
        }

        protected override bool RegionProcessor(Region region)
        {
            List<Thing> list = region.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways));
            ValidItems.AddRange(list.Where(ItemValidator));
            return false;
        }

        private bool ItemValidator(Thing item)
        {
            if (!item.Spawned || (itemFilter != null && !itemFilter.Allows(item)) || item.IsForbidden(worker) || item.IsBurning() || !worker.CanReserve(item))
            {
                return false;
            }
            if ((double)(item.Position - anchorCell).LengthHorizontalSquared >= searchRadiusSquared)
            {
                return false;
            }
            if (!item.TryGetQuality(out QualityCategory quality))
            {
                return false;
            }
            if(this.includeMaxQuality)
            {
                return quality <= this.maxQuality;
            }
            return quality < this.maxQuality;
        }
    }
}
