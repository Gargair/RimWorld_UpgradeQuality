using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    internal class WorkGiver_UpgradeQuality_Item : WorkGiver_DoBill
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
                    var possibleIngredients = GetAllPossibleIngredients(bill, pawn, bench);
                    DefCountList AvailableCounts = new DefCountList();
                    AvailableCounts.GenerateFrom(possibleIngredients);
                    HashSet<Thing> AssignedThings = new HashSet<Thing>();
                    foreach (Thing itemToUpgrade in list)
                    {
                        AssignedThings.Clear();
                        if (TryFindBestBillIngredients(possibleIngredients, this.chosenIngThings, itemToUpgrade, AvailableCounts, AssignedThings))
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
            bool validator(Thing t)
            {
                if (!t.IsForbidden(pawn) && t is UnfinishedUpgrade)
                {
                    return pawn.CanReserve(t, 1, 1);
                }
                return false;
            }
            return GenClosest.ClosestThingReachable(bench.Position, bench.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.InteractionCell, TraverseParms.For(pawn, pawn.NormalMaxDanger()), bill.ingredientSearchRadius, validator);
        }

        private static List<Thing> FindItemsToUpgrade(Pawn pawn, Thing bench, Bill bill)
        {
            Region validRegionAt = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(bench, pawn));
            if (validRegionAt == null)
            {
                return new List<Thing>();
            }
            var proc = new RegionProcessor_ThingToUpgrade(pawn, bill.ingredientSearchRadius, GetBillGiverRootCell(bench, pawn), bill.ingredientFilter, true);
            RegionTraverser.BreadthFirstTraverse(validRegionAt, proc, 99999, RegionType.Set_Passable);
            proc.Sort();
            return proc.ValidItems;
        }

        //public override bool ThingIsUsableBillGiver(Thing thing)
        //{
        //    return (this.def.fixedBillGiverDefs != null && this.def.fixedBillGiverDefs.Contains(thing.def));
        //}

        private static List<Thing> GetAllPossibleIngredients(Bill bill, Pawn pawn, Thing billGiver)
        {
            Region validRegionAt = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(billGiver, pawn));
            if (validRegionAt == null)
            {
                return new List<Thing>();
            }
            var proc = new RegionProcessor_ThingToUpgrade(pawn, bill.ingredientSearchRadius, GetBillGiverRootCell(billGiver, pawn), null, false);
            RegionTraverser.BreadthFirstTraverse(validRegionAt, proc, 99999, RegionType.Set_Passable);
            proc.Sort();
            return proc.ValidItems;
        }

        private static bool TryFindBestBillIngredients(List<Thing> possibleItems, List<ThingCount> chosen, Thing itemToUpgrade, DefCountList AvailableCounts, HashSet<Thing> AssignedThings)
        {
            chosen.Clear();
            List<ThingDefCountQuality> neededIngreds = UpgradeQualityUtility.GetNeededResources(itemToUpgrade);

            if (neededIngreds.NullOrEmpty())
            {
                return true;
            }
            return TryFindBestBillIngredientsInSet_NoMix(possibleItems, neededIngreds, chosen, itemToUpgrade, AvailableCounts, AssignedThings);
        }

        private static bool TryFindBestBillIngredientsInSet_NoMix(List<Thing> availableThings, List<ThingDefCountQuality> neededIngreds, List<ThingCount> chosen, Thing thingToIgnore, DefCountList AvailableCounts, HashSet<Thing> AssignedThings)
        {
            chosen.Clear();
            foreach (ThingDefCountQuality ingredientToFind in neededIngreds)
            {
                bool foundIngredient = false;
                for (int i = 0; i < AvailableCounts.Count; i++)
                {
                    float remainingIngredientCount = ingredientToFind.Count;
                    bool availableIsIngredient = remainingIngredientCount <= AvailableCounts.GetCount(i) && ingredientToFind.ThingDef == AvailableCounts.GetDef(i);
                    if (availableIsIngredient)
                    {
                        foreach (Thing thing in availableThings)
                        {
                            if (thing == thingToIgnore)
                            {
                                continue;
                            }
                            bool thingIsIngredientNotUsed = thing.def == AvailableCounts.GetDef(i) && !AssignedThings.Contains(thing);
                            if (thingIsIngredientNotUsed)
                            {
                                var shouldAdd = false;
                                if (thing.TryGetQuality(out QualityCategory qCat))
                                {
                                    if (ingredientToFind.Range.Includes(qCat))
                                    {
                                        shouldAdd = true;
                                    }
                                }
                                else if (ingredientToFind.Range == QualityRange.All)
                                {
                                    shouldAdd = true;
                                }
                                if (shouldAdd)
                                {
                                    int usedCount = Mathf.Min(Mathf.FloorToInt(remainingIngredientCount), thing.stackCount);
                                    ThingCountUtility.AddToList(chosen, thing, usedCount);
                                    remainingIngredientCount -= usedCount;
                                    AssignedThings.Add(thing);
                                    bool ingredientFullyFound = remainingIngredientCount < 0.001f;
                                    if (ingredientFullyFound)
                                    {
                                        foundIngredient = true;
                                        float remainingAvailableCount = AvailableCounts.GetCount(i) - ingredientToFind.Count;
                                        AvailableCounts.SetCount(i, remainingAvailableCount);
                                        break;
                                    }
                                }
                            }
                        }
                        if (foundIngredient)
                        {
                            break;
                        }
                    }
                }
                if (!foundIngredient)
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
                    this[def] += thing.stackCount;
                }
            }

            private readonly List<ThingDef> _defs;

            private readonly List<float> _counts;
        }
    }
}
