using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    internal class WorkGiver_UpgradeQuality_Item : WorkGiver_Scanner
    {
        public WorkGiver_UpgradeQuality_Item()
        {
            this.chosenIngThings = new List<ThingCount>();
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.InteractionCell;
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Some;
        }

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                bool flag = this.def.fixedBillGiverDefs != null && this.def.fixedBillGiverDefs.Count == 1;
                ThingRequest result;
                if (flag)
                {
                    result = ThingRequest.ForDef(this.def.fixedBillGiverDefs[0]);
                }
                else
                {
                    result = ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);
                }
                return result;
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing bench, bool forced = false)
        {
            IBillGiver billGiver = bench as IBillGiver;
            if (billGiver == null || !this.ThingIsUsableBillGiver(bench) || !billGiver.CurrentlyUsableForBills() || !billGiver.BillStack.AnyShouldDoNow || bench.IsBurning() || bench.IsForbidden(pawn))
            {
                return null;
            }

            if (!pawn.CanReserve(bench, 1, -1, null, false))
            {
                return null;
            }
            if (!pawn.CanReserveAndReach(bench.InteractionCell, PathEndMode.OnCell, Danger.Some, 1, -1, null, false))
            {
                return null;
            }
            billGiver.BillStack.RemoveIncompletableBills();
            Job job = WorkGiverUtility.HaulStuffOffBillGiverJob(pawn, billGiver, null);
            if (job != null)
            {
                return job;
            }
            foreach (Bill bill in billGiver.BillStack)
            {
                bool shouldSkip = (bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != this.def.workType) || (Find.TickManager.TicksGame < bill.nextTickToSearchForIngredients && FloatMenuMakerMap.makingFor != pawn) || !bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn);
                if (!shouldSkip)
                {
                    if (!bill.recipe.PawnSatisfiesSkillRequirements(pawn))
                    {
                        JobFailReason.Is("MissingSkill".Translate(), null);
                        return null;
                    }
                    var unfinishedThing = FindUnfinishedUpgradeThing(pawn, bench, bill);
                    if (unfinishedThing != null)
                    {
                        return StartNewUpgradeJob(bill, billGiver, unfinishedThing, new List<ThingCount>());
                    }
                    List<Thing> list = FindItemsToUpgrade(pawn, bench, bill);
                    if (list.NullOrEmpty())
                    {
                        JobFailReason.Is("UpgQlty.Messages.NoUpgradeItems".Translate(), null);
                        return null;
                    }
                    foreach (Thing itemToUpgrade in list)
                    {
                        if (TryFindBestBillIngredients(bill, pawn, bench, this.chosenIngThings, itemToUpgrade))
                        {
                            return StartNewUpgradeJob(bill, billGiver, itemToUpgrade, this.chosenIngThings);
                        }
                    }
                }
            }
            JobFailReason.Is("UpgQlty.Messages.NoUpgradeItems".Translate(), null);
            return null;
        }

        private static Thing FindUnfinishedUpgradeThing(Pawn pawn, Thing bench, Bill bill)
        {
            Region validRegionAt = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(bench, pawn));
            if (validRegionAt == null)
            {
                return null;
            }
            RegionEntryPredicate entryCondition = (Region from, Region to) => to.Allows(TraverseParms.For(pawn), false);
            Func<Thing, bool> itemValidator = delegate (Thing item)
            {
                if (!item.Spawned || item.IsForbidden(pawn) || item.IsBurning() || !pawn.CanReserve(item))
                {
                    return false;
                }
                if ((double)(item.Position - bench.Position).LengthHorizontalSquared >= (double)bill.ingredientSearchRadius * (double)bill.ingredientSearchRadius)
                {
                    return false;
                }
                return item is UnfinishedUpgrade;
            };
            Thing foundUnfinishedThing = null;
            RegionProcessor regionProcessor = delegate (Region region)
            {
                List<Thing> list = region.ListerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
                var unfinishedThings = list.Where(itemValidator);
                if (unfinishedThings.Count() > 0)
                {
                    foundUnfinishedThing = unfinishedThings.First();
                    return true;
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(validRegionAt, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            return foundUnfinishedThing;
        }

        private static List<Thing> FindItemsToUpgrade(Pawn pawn, Thing bench, Bill bill)
        {
            Region validRegionAt = pawn.Map.regionGrid.GetValidRegionAt(WorkGiver_UpgradeQuality_Item.GetBillGiverRootCell(bench, pawn));
            if (validRegionAt == null)
            {
                return new List<Thing>();
            }
            List<Thing> validItems = new List<Thing>();
            List<Thing> relevantItems = new List<Thing>();
            RegionEntryPredicate entryCondition = (Region from, Region to) => to.Allows(TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), false);
            Func<Thing, bool> itemValidator = delegate (Thing item)
            {
                if (!item.Spawned || !bill.ingredientFilter.Allows(item) || item.IsForbidden(pawn) || item.IsBurning() || !pawn.CanReserve(item))
                {
                    return false;
                }
                if ((double)(item.Position - bench.Position).LengthHorizontalSquared >= (double)bill.ingredientSearchRadius * (double)bill.ingredientSearchRadius)
                {
                    return false;
                }
                //if (item.CostListAdjusted().Count == 0)
                //{
                //    return false;
                //}
                var compQuality = item.TryGetComp<CompQuality>();
                if (compQuality == null || compQuality.Quality >= QualityCategory.Legendary)
                {
                    return false;
                }
                return true;
            };
            RegionProcessor regionProcessor = delegate (Region region)
            {
                List<Thing> list = region.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways));
                relevantItems.AddRange(list.Where(itemValidator));
                if (relevantItems.Count > 0)
                {
                    relevantItems.Sort((Thing t1, Thing t2) => (t1.Position - pawn.Position).LengthHorizontalSquared.CompareTo((t2.Position - pawn.Position).LengthHorizontalSquared));
                    validItems.AddRange(relevantItems);
                    relevantItems.Clear();
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(validRegionAt, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            return validItems;
        }

        private bool ThingIsUsableBillGiver(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            Corpse corpse = thing as Corpse;
            Pawn pawn2 = null;
            bool flag = corpse != null;
            if (flag)
            {
                pawn2 = corpse.InnerPawn;
            }
            return (this.def.fixedBillGiverDefs != null && this.def.fixedBillGiverDefs.Contains(thing.def)) || (pawn != null && ((this.def.billGiversAllHumanlikes && pawn.RaceProps.Humanlike) || (this.def.billGiversAllMechanoids && pawn.RaceProps.IsMechanoid) || (this.def.billGiversAllAnimals && pawn.RaceProps.Animal))) || (corpse != null && pawn2 != null && ((this.def.billGiversAllHumanlikesCorpses && pawn2.RaceProps.Humanlike) || (this.def.billGiversAllMechanoidsCorpses && pawn2.RaceProps.IsMechanoid) || (this.def.billGiversAllAnimalsCorpses && pawn2.RaceProps.Animal)));
        }

        private static bool TryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, Thing itemDamaged)
        {
            chosen.Clear();
            List<ThingDefCountQuality> neededIngreds = CalculateTotalIngredients(itemDamaged);

            if (neededIngreds.NullOrEmpty())
            {
                return true;
            }
            Region validRegionAt = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(billGiver, pawn));
            if (validRegionAt == null)
            {
                return false;
            }

            List<Thing> relevantThings = new List<Thing>();
            bool foundAll = false;

            Func<Thing, bool> baseValidator = delegate (Thing t)
            {
                if (!t.Spawned || t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, false))
                {
                    return false;
                }
                if ((double)(t.Position - billGiver.Position).LengthHorizontalSquared >= (double)bill.ingredientSearchRadius * (double)bill.ingredientSearchRadius)
                {
                    return false;
                }
                if (!neededIngreds.Any((ThingDefCountQuality ingred) => ingred.ThingDef == t.def))
                {
                    return false;
                }
                if(t == itemDamaged)
                {
                    return false;
                }
                return (!bill.CheckIngredientsIfSociallyProper || t.IsSociallyProper(pawn));
            };

            bool billGiverIsPawn = billGiver is Pawn;
            List<Thing> newRelevantThings = new List<Thing>();

            RegionProcessor regionProcessor = delegate (Region r)
            {
                newRelevantThings.Clear();
                List<Thing> list = r.ListerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
                foreach (Thing thing in list)
                {
                    if (baseValidator(thing) && (!thing.def.IsMedicine || !billGiverIsPawn))
                    {
                        newRelevantThings.Add(thing);
                    }
                }

                if (newRelevantThings.Count <= 0)
                {
                    return false;
                }
                newRelevantThings.Sort((Thing t1, Thing t2) => (t1.Position - pawn.Position).LengthHorizontalSquared.CompareTo((t2.Position - pawn.Position).LengthHorizontalSquared));
                relevantThings.AddRange(newRelevantThings);
                newRelevantThings.Clear();
                if (TryFindBestBillIngredientsInSet_NoMix(relevantThings, neededIngreds, chosen))
                {
                    foundAll = true;
                    return true;
                }
                return false;
            };
            RegionEntryPredicate entryCondition = (Region from, Region to) => to.Allows(TraverseParms.For(pawn), false);
            RegionTraverser.BreadthFirstTraverse(validRegionAt, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            return foundAll;
        }

        internal static List<ThingDefCountQuality> CalculateTotalIngredients(Thing itemDamaged)
        {
            var list = itemDamaged.CostListAdjusted();
            var qComp = itemDamaged.TryGetComp<CompQuality>();
            if (qComp == null)
            {
                return new List<ThingDefCountQuality>();
            }
            if (list.NullOrEmpty())
            {
                var ret = new List<ThingDefCountQuality>();
                ret.Add(new ThingDefCountQuality(itemDamaged.def, 1, new QualityRange(qComp.Quality, qComp.Quality)));
                return ret;
            }
            var mult = UpgradeQualityUtility.GetMultiplier(qComp.Quality);
            return list.Select(t => new ThingDefCountQuality(t.thingDef, Mathf.CeilToInt(t.count * mult))).ToList();
        }

        private static bool TryFindBestBillIngredientsInSet_NoMix(List<Thing> availableThings, List<ThingDefCountQuality> neededIngreds, List<ThingCount> chosen)
        {
            chosen.Clear();
            var AvailableCounts = new DefCountList();
            var AssignedThings = new HashSet<Thing>();
            AvailableCounts.GenerateFrom(availableThings);
            foreach (ThingDefCountQuality thingDefCount in neededIngreds)
            {
                bool flag = false;
                for (int i = 0; i < AvailableCounts.Count; i++)
                {
                    float num = (float)thingDefCount.Count;
                    bool flag2 = (double)num > (double)AvailableCounts.GetCount(i) || thingDefCount.ThingDef != AvailableCounts.GetDef(i);
                    if (!flag2)
                    {
                        foreach (Thing thing in availableThings)
                        {
                            bool flag3 = thing.def != AvailableCounts.GetDef(i) || AssignedThings.Contains(thing);
                            if (!flag3)
                            {
                                var shouldAdd = false;
                                if (thing.TryGetQuality(out QualityCategory qCat))
                                {
                                    if (thingDefCount.Range.Includes(qCat))
                                    {
                                        shouldAdd = true;
                                    }
                                }
                                else if (thingDefCount.Range == QualityRange.All)
                                {
                                    shouldAdd = true;
                                }
                                if (shouldAdd)
                                {
                                    int num2 = Mathf.Min(Mathf.FloorToInt(num), thing.stackCount);
                                    ThingCountUtility.AddToList(chosen, thing, num2);
                                    num -= (float)num2;
                                    AssignedThings.Add(thing);
                                    bool flag4 = (double)num < 0.001;
                                    if (flag4)
                                    {
                                        flag = true;
                                        float val = AvailableCounts.GetCount(i) - (float)thingDefCount.Count;
                                        AvailableCounts.SetCount(i, val);
                                        break;
                                    }
                                }
                            }
                        }
                        bool flag5 = flag;
                        if (flag5)
                        {
                            break;
                        }
                    }
                }
                bool flag6 = !flag;
                if (flag6)
                {
                    return false;
                }
            }
            return true;
        }

        private static Job StartNewUpgradeJob(Bill bill, IBillGiver workbench, Thing itemToUpgrade, IList<ThingCount> ingredients)
        {
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Starting new upgrade job", bill.recipe.defName, bill.GetType().FullName, itemToUpgrade.def.defName, ingredients.ToString());
            Job job = new Job(UpgradeQualityDefOf.Jobs.IncreaseQuality_Job, (Thing)workbench)
            {
                haulMode = HaulMode.ToCellNonStorage,
                bill = bill,
                targetQueueB = new List<LocalTargetInfo>(ingredients.Count + 1),
                countQueue = new List<int>(ingredients.Count + 1)
            };
            job.targetQueueB.Add(itemToUpgrade);
            job.countQueue.Add(1);
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Count > 0)
                {
                    job.targetQueueB.Add(ingredients[i].Thing);
                    job.countQueue.Add(ingredients[i].Count);
                }
            }
            return job;
        }

        private static IntVec3 GetBillGiverRootCell(Thing billGiver, Pawn forPawn)
        {
            Verse.Building building = billGiver as Verse.Building;
            if (building == null)
            {
                return billGiver.Position;
            }
            else if (building.def.hasInteractionCell)
            {
                return building.InteractionCell;
            }
            else
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Error, "Tried to find bill ingredients for", ((billGiver != null) ? billGiver.ToString() : "<null>"), "which has no interaction cell.");
                return forPawn.Position;
            }
        }

        private readonly List<ThingCount> chosenIngThings;


        private class DefCountList
        {
            public int Count
            {
                get
                {
                    return this._defs.Count;
                }
            }

            private float this[ThingDef def]
            {
                get
                {
                    int num = this._defs.IndexOf(def);
                    bool flag = num < 0;
                    float result;
                    if (flag)
                    {
                        result = 0f;
                    }
                    else
                    {
                        result = this._counts[num];
                    }
                    return result;
                }
                set
                {
                    int num = this._defs.IndexOf(def);
                    bool flag = num < 0;
                    if (flag)
                    {
                        this._defs.Add(def);
                        this._counts.Add(value);
                        num = this._defs.Count - 1;
                    }
                    else
                    {
                        this._counts[num] = value;
                    }
                    this.CheckRemove(num);
                }
            }

            public DefCountList()
            {
                this._defs = new List<ThingDef>();
                this._counts = new List<float>();
            }

            public float GetCount(int index)
            {
                return this._counts[index];
            }

            public void SetCount(int index, float val)
            {
                this._counts[index] = val;
                this.CheckRemove(index);
            }

            public ThingDef GetDef(int index)
            {
                return this._defs[index];
            }

            private void CheckRemove(int index)
            {
                bool flag = Math.Abs(this._counts[index]) > 0.001f;
                if (!flag)
                {
                    this._counts.RemoveAt(index);
                    this._defs.RemoveAt(index);
                }
            }

            public void Clear()
            {
                this._defs.Clear();
                this._counts.Clear();
            }

            public void GenerateFrom(List<Thing> things)
            {
                this.Clear();
                foreach (Thing thing in things)
                {
                    ThingDef def = thing.def;
                    this[def] += (float)thing.stackCount;
                }
            }

            private readonly List<ThingDef> _defs;

            private readonly List<float> _counts;
        }
    }
}
