using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    public class RegionProcessor_ThingToUpgrade : RegionProcessorDelegateCache
    {
        private Pawn worker;
        private double searchRadiusSquared;
        private IntVec3 anchorCell;
        private ThingFilter itemFilter;
        private bool noLegendary;

        public List<Thing> ValidItems = new List<Thing>();

        public RegionProcessor_ThingToUpgrade(Pawn worker, double searchRadius, IntVec3 anchorCell, ThingFilter itemFilter, bool noLegendary)
        {
            this.worker = worker;
            this.searchRadiusSquared = searchRadius * searchRadius;
            this.anchorCell = anchorCell;
            this.itemFilter = itemFilter;
            this.noLegendary = noLegendary;
        }

        public void Reset()
        {
            this.ValidItems.Clear();
        }

        protected override bool RegionEntryPredicate(Region from, Region to)
        {
            return to.Allows(TraverseParms.For(worker), false);
        }

        public void Sort()
        {
            ValidItems.Sort((Thing t1, Thing t2) => (t1.Position - anchorCell).LengthHorizontalSquared.CompareTo((t2.Position - anchorCell).LengthHorizontalSquared));
        }

        protected override bool RegionProcessor(Region region)
        {
            List<Thing> list = region.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways));
            ValidItems.AddRange(list.Where(ItemValidator));
            if (region.IsDoorway && (region.AnyCell - anchorCell).LengthHorizontalSquared >= (searchRadiusSquared))
            {
                return true;
            }
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
            if (noLegendary && (!item.TryGetQuality(out QualityCategory qc) || qc >= QualityCategory.Legendary))
            {
                return false;
            }
            return true;
        }
    }
}
