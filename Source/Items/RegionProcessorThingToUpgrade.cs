using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    public class RegionProcessorThingToUpgrade : RegionProcessorDelegateCache
    {
        private readonly Pawn worker;
        private readonly double searchRadius;
        private readonly double searchRadiusSquared;
        private readonly IntVec3 anchorCell;
        private readonly ThingFilter itemFilter;

        public List<Thing> ValidItems { get; } = new List<Thing>();

        public RegionProcessorThingToUpgrade(Pawn worker, double searchRadius, IntVec3 anchorCell, ThingFilter itemFilter)
        {
            this.worker = worker;
            this.searchRadius = searchRadius;
            this.searchRadiusSquared = searchRadius * searchRadius;
            this.anchorCell = anchorCell;
            this.itemFilter = itemFilter;
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

        protected override bool RegionProcessor(Region reg)
        {
            List<Thing> list = reg.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways));
            ValidItems.AddRange(list.Where(ItemValidator));
            return false;
        }

        private bool ItemValidator(Thing item)
        {
            if (!item.Spawned)
            {
                return false;
            }
            if (item.IsForbidden(worker))
            {
                return false;
            }
            if (item.IsBurning())
            {
                return false;
            }
            if (itemFilter != null && !itemFilter.Allows(item))
            {
                return false;
            }
            if (!worker.CanReserve(item))
            {
                return false;
            }
            if ((double)(item.Position - anchorCell).LengthHorizontalSquared >= searchRadiusSquared)
            {
                return false;
            }
            return true;
        }
    }
}
