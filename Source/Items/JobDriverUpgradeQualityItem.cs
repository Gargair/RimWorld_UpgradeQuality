using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    public class JobDriverUpgradeQualityItem : JobDriver_DoBill
    {
        private Thing cachedThingToUpgrade;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
            if (!this.pawn.Reserve(this.job.GetTarget(TargetIndex.A), this.job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            if (thing != null && thing.def.hasInteractionCell && !this.pawn.ReserveSittableOrSpot(thing.InteractionCell, this.job, errorOnFailed))
            {
                return false;
            }
            this.pawn.ReserveAsManyAsPossible(this.job.GetTargetQueue(TargetIndex.B), this.job, 1, -1, null);
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref cachedThingToUpgrade, "cachedThingToUpgrade");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            base.AddEndCondition(delegate
            {
                Thing thing = base.GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                if (thing is Verse.Building && !thing.Spawned)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(delegate ()
            {
                if (this.job.GetTarget(TargetIndex.A).Thing is IBillGiver billGiver)
                {
                    if (this.job.bill.DeletedOrDereferenced)
                    {
                        return true;
                    }
                    if (!billGiver.CurrentlyUsableForBills())
                    {
                        return true;
                    }
                }
                return false;
            });
            Toil gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate ()
            {
                if (this.job.targetQueueB != null &&
                    this.job.targetQueueB.Count == 1 &&
                    this.job.targetQueueB[0].Thing is UnfinishedThing unfinishedThing)
                {
                    unfinishedThing.BoundBill = (Bill_ProductionWithUft)this.job.bill;
                }

                this.job.bill.Notify_DoBillStarted(this.pawn);
            };
            yield return toil;
            yield return Toils_Jump.JumpIf(gotoBillGiver, () => this.job.GetTargetQueue(TargetIndex.B).NullOrEmpty<LocalTargetInfo>());
            if (cachedThingToUpgrade == null)
            {
                cachedThingToUpgrade = job.GetTargetQueue(TargetIndex.B).FirstOrDefault().Thing;
            }
            foreach (Toil toil2 in JobDriver_DoBill.CollectIngredientsToils(TargetIndex.B, TargetIndex.A, TargetIndex.C, false, true, false))
            {
                yield return toil2;
            }
            yield return gotoBillGiver;
            yield return MakeUnfinishedThingIfNeeded(cachedThingToUpgrade);
            yield return Toils_Recipe.DoRecipeWork().WithProgressBar(TargetIndex.A, delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.CurJob;
                Thing thing = curJob.GetTarget(TargetIndex.B).Thing;
                float workLeft = ((JobDriver_DoBill)actor.jobs.curDriver).workLeft;
                if (thing is UnfinishedUpgrade unfinishedUpgrade)
                {
                    var thingToUpgrade = unfinishedUpgrade.ThingToUpgrade;
                    float multiplier = 1f;
                    if (thingToUpgrade.TryGetComp<CompQuality>(out var qualityComp))
                    {
                        multiplier = UpgradeQualityUtility.GetMultiplier(qualityComp.Quality);
                    }
                    float num = thingToUpgrade.def.GetStatValueAbstract(StatDefOf.WorkToMake, thingToUpgrade.Stuff) * multiplier;
                    return 1f - workLeft / num;
                }
                else
                {
                    float num = curJob.bill.recipe.WorkAmountTotal(thing);
                    return 1f - workLeft / num;
                }
            }).FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
            yield return FinishRecipeAndStartStoringProduct(TargetIndex.None);
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, Toils_Haul.DropCarriedThing(), true, true);
        }

        private static Toil MakeUnfinishedThingIfNeeded(Thing thingToUpgrade)
        {
            Toil toil = ToilMaker.MakeToil("MakeUnfinishedThingIfNeeded");
            toil.initAction = delegate ()
            {
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Init MakeUnfinishedThingIfNeeded");
#endif
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (!curJob.RecipeDef.UsesUnfinishedThing)
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage("Not using unfinished thing");
#endif
                    return;
                }
                if (curJob.GetTarget(TargetIndex.B).Thing is UnfinishedThing)
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage("Already have unfinished thing");
#endif
                    return;
                }
                List<Thing> list = CalculateIngredients(curJob, actor);
                bool hasSomeThingSelected = Find.Selector.IsSelected(thingToUpgrade);
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Calculated ingredients");
#endif
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing2 = list[i];
                    actor.Map.designationManager.RemoveAllDesignationsOn(thing2, false);
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage("Despawning", thing2.GetUniqueLoadID());
#endif
                    hasSomeThingSelected = hasSomeThingSelected || Find.Selector.IsSelected(thing2);
                    thing2.DeSpawnOrDeselect(DestroyMode.Vanish);
                }
                UnfinishedUpgrade unfinishedThing = (UnfinishedUpgrade)ThingMaker.MakeThing(curJob.RecipeDef.unfinishedThingDef);
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Created unfinished thing");
                UpgradeQualityUtility.LogMessage(curJob.bill.GetType().FullName);
#endif
                float multiplier = 1f;
                if (thingToUpgrade.TryGetComp<CompQuality>(out var qualityComp))
                {
                    multiplier = UpgradeQualityUtility.GetMultiplier(qualityComp.Quality);
                }
                unfinishedThing.Creator = actor;
                unfinishedThing.BoundBill = (Bill_ProductionWithUft)curJob.bill;
                unfinishedThing.ingredients = list.Where(t => t != thingToUpgrade).ToList();
                unfinishedThing.workLeft = thingToUpgrade.def.GetStatValueAbstract(StatDefOf.WorkToMake, thingToUpgrade.Stuff) * multiplier;
                unfinishedThing.ThingToUpgrade = thingToUpgrade;
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Spawning unfinished thing");
#endif
                GenSpawn.Spawn(unfinishedThing, curJob.GetTarget(TargetIndex.A).Cell, actor.Map, WipeMode.Vanish);
#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage("Spawned unfinished thing");
#endif
                curJob.SetTarget(TargetIndex.B, unfinishedThing);
                actor.Reserve(unfinishedThing, curJob, 1, -1, null, true);
                if (hasSomeThingSelected)
                {
                    Find.Selector.Select(unfinishedThing);
                }
            };
            return toil;
        }

        private static List<Thing> CalculateIngredients(Job job, Pawn actor)
        {
            if (job.GetTarget(TargetIndex.B).Thing is UnfinishedThing unfinishedThing)
            {
                List<Thing> ingredients = unfinishedThing.ingredients;
                job.RecipeDef.Worker.ConsumeIngredient(unfinishedThing, job.RecipeDef, actor.Map);
                job.placedThings = null;
                return ingredients;
            }
            List<Thing> list = new List<Thing>();
            if (job.placedThings != null)
            {
                for (int i = 0; i < job.placedThings.Count; i++)
                {
                    CheckPlacedThing(job, list, i);
                }
            }
            job.placedThings = null;
            return list;
        }

        private static void CheckPlacedThing(Job job, List<Thing> list, int i)
        {
            if (job.placedThings[i].Count <= 0)
            {
                UpgradeQualityUtility.LogError("PlacedThing ", job.placedThings[i], " with count ", job.placedThings[i].Count, " for job ", job);
            }
            else
            {
                Thing thing;
                if (job.placedThings[i].Count < job.placedThings[i].thing.stackCount)
                {
                    thing = job.placedThings[i].thing.SplitOff(job.placedThings[i].Count);
                }
                else
                {
                    thing = job.placedThings[i].thing;
                }
                job.placedThings[i].Count = 0;
                if (list.Contains(thing))
                {
                    UpgradeQualityUtility.LogError("Tried to add ingredient from job placed targets twice:", thing);
                }
                else
                {
                    list.Add(thing);
                    if (job.RecipeDef.autoStripCorpses &&
                        thing is IStrippable strippable &&
                        strippable.AnythingToStrip())
                    {
                        strippable.Strip();
                    }
                }
            }
        }

        const string FinishRecipeAndStartStoringProductDebugName = "FinishRecipeAndStartStoringProduct";
        private static Toil FinishRecipeAndStartStoringProduct(TargetIndex productIndex = TargetIndex.A)
        {
            Toil toil = ToilMaker.MakeToil(FinishRecipeAndStartStoringProductDebugName);
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                UnfinishedUpgrade unfinishedThing = curJob.GetTarget(TargetIndex.B).Thing as UnfinishedUpgrade;
                var thingToUpgrade = unfinishedThing.ThingToUpgrade;
                bool hasUnfinishedThingSelected = Find.Selector.IsSelected(unfinishedThing);
                List<Thing> ingredients = CalculateIngredients(curJob, actor);

#if DEBUG && DEBUGITEMS
                UpgradeQualityUtility.LogMessage(FinishRecipeAndStartStoringProductDebugName, "hasUnfinishedThingSelected", hasUnfinishedThingSelected);
#endif
                curJob.bill.Notify_IterationCompleted(actor, ingredients);
                HandleArtFromQuality(actor, thingToUpgrade);

                if (HandleDropOnFloor(actor, curJob, thingToUpgrade, hasUnfinishedThingSelected))
                {
                    return;
                }
                IntVec3 targetCell = IntVec3.Invalid;
                if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellFor(thingToUpgrade, actor, actor.Map, StoragePriority.Unstored, actor.Faction, out targetCell, true);
                }
                else if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellForIn(thingToUpgrade, actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetSlotGroup(), out targetCell, true);
                }
                else
                {
                    Log.ErrorOnce("Unknown store mode", 9158246);
                }
                if (targetCell.IsValid)
                {
                    actor.carryTracker.TryStartCarry(thingToUpgrade);
                    curJob.targetB = targetCell;
                    if (productIndex != TargetIndex.None)
                    {
                        curJob.SetTarget(productIndex, thingToUpgrade);
                    }
                    curJob.count = 99999;
                    if (hasUnfinishedThingSelected)
                    {
#if DEBUG && DEBUGITEMS
                        UpgradeQualityUtility.LogMessage(FinishRecipeAndStartStoringProductDebugName, "selecting3");
#endif
                        Find.Selector.Select(thingToUpgrade);
                    }
                    return;
                }
                if (!GenPlace.TryPlaceThing(thingToUpgrade, actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                {
                    UpgradeQualityUtility.LogError(actor, "could not drop product", thingToUpgrade, "near", actor.Position);
                }
                if (hasUnfinishedThingSelected)
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage(FinishRecipeAndStartStoringProductDebugName, "selecting4");
#endif
                    Find.Selector.Select(thingToUpgrade);
                }
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
            };
            return toil;
        }

        private static bool HandleDropOnFloor(Pawn actor, Job curJob, Thing thingToUpgrade, bool hasUnfinishedThingSelected)
        {
            if (curJob == null || curJob.bill == null || curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
            {
                if (!GenPlace.TryPlaceThing(thingToUpgrade, actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                {
                    UpgradeQualityUtility.LogError(actor, "could not drop recipe product", thingToUpgrade, "near", actor.Position);
                }
                if (hasUnfinishedThingSelected)
                {
#if DEBUG && DEBUGITEMS
                    UpgradeQualityUtility.LogMessage(FinishRecipeAndStartStoringProductDebugName, "selecting1");
#endif
                    Find.Selector.Select(thingToUpgrade);
                }
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
                return true;
            }
            return false;
        }

        private static void HandleArtFromQuality(Pawn actor, Thing thingToUpgrade)
        {
            var qComp = thingToUpgrade.TryGetComp<CompQuality>();
            if (qComp != null && qComp.Quality < QualityCategory.Legendary)
            {
                qComp.SetQuality(qComp.Quality + 1, ArtGenerationContext.Colony);
                var artComp = thingToUpgrade.TryGetComp<CompArt>();
                artComp?.JustCreatedBy(actor);
            }
        }
    }
}
