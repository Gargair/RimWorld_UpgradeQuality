using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace UpgradeQuality.Items
{
    public class JobDriver_UpgradeQuality_Item : JobDriver_DoBill
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
                IBillGiver billGiver = this.job.GetTarget(TargetIndex.A).Thing as IBillGiver;
                if (billGiver != null)
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
                if (this.job.targetQueueB != null && this.job.targetQueueB.Count == 1)
                {
                    UnfinishedThing unfinishedThing = this.job.targetQueueB[0].Thing as UnfinishedThing;
                    if (unfinishedThing != null)
                    {
                        unfinishedThing.BoundBill = (Bill_ProductionWithUft)this.job.bill;
                    }
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
            yield return Toils_Recipe.DoRecipeWork().FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
            yield return FinishRecipeAndStartStoringProduct(TargetIndex.None);
            yield break;
        }

        private static Toil MakeUnfinishedThingIfNeeded(Thing thingToUpgrade)
        {
            Toil toil = ToilMaker.MakeToil("MakeUnfinishedThingIfNeeded");
            toil.initAction = delegate ()
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Init MakeUnfinishedThingIfNeeded");
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (!curJob.RecipeDef.UsesUnfinishedThing)
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Not using unfinished thing");
                    return;
                }
                if (curJob.GetTarget(TargetIndex.B).Thing is UnfinishedThing)
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Already have unfinished thing");
                    return;
                }
                List<Thing> list = CalculateIngredients(curJob, actor);
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Calculated ingredients");
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing2 = list[i];
                    actor.Map.designationManager.RemoveAllDesignationsOn(thing2, false);
                    UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Despawning", thing2.GetUniqueLoadID());
                    thing2.DeSpawnOrDeselect(DestroyMode.Vanish);
                }
                UnfinishedUpgrade unfinishedThing = (UnfinishedUpgrade)ThingMaker.MakeThing(curJob.RecipeDef.unfinishedThingDef);
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Created unfinished thing");
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, curJob.bill.GetType().FullName);
                unfinishedThing.Creator = actor;
                unfinishedThing.BoundBill = (Bill_ProductionWithUft)curJob.bill;
                unfinishedThing.ingredients = list.Where(t => t != thingToUpgrade).ToList();
                unfinishedThing.workLeft = curJob.bill.GetWorkAmount(unfinishedThing);
                unfinishedThing.thingToUpgrade = thingToUpgrade;
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Spawning unfinished thing");
                GenSpawn.Spawn(unfinishedThing, curJob.GetTarget(TargetIndex.A).Cell, actor.Map, WipeMode.Vanish);
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Spawned unfinished thing");
                curJob.SetTarget(TargetIndex.B, unfinishedThing);
                actor.Reserve(unfinishedThing, curJob, 1, -1, null, true);
            };
            return toil;
        }

        private static List<Thing> CalculateIngredients(Job job, Pawn actor)
        {
            UnfinishedThing unfinishedThing = job.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
            if (unfinishedThing != null)
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
                    if (job.placedThings[i].Count <= 0)
                    {
                        Log.Error(string.Concat(new object[]
                        {
                            "PlacedThing ",
                            job.placedThings[i],
                            " with count ",
                            job.placedThings[i].Count,
                            " for job ",
                            job
                        }));
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
                            Log.Error("Tried to add ingredient from job placed targets twice: " + thing);
                        }
                        else
                        {
                            list.Add(thing);
                            if (job.RecipeDef.autoStripCorpses)
                            {
                                IStrippable strippable = thing as IStrippable;
                                if (strippable != null && strippable.AnythingToStrip())
                                {
                                    strippable.Strip();
                                }
                            }
                        }
                    }
                }
            }
            job.placedThings = null;
            return list;
        }

        private static Toil FinishRecipeAndStartStoringProduct(TargetIndex productIndex = TargetIndex.A)
        {
            Toil toil = ToilMaker.MakeToil("FinishRecipeAndStartStoringProduct");
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_UpgradeQuality_Item jobDriver_DoBill = (JobDriver_UpgradeQuality_Item)actor.jobs.curDriver;
                UnfinishedUpgrade unfinishedThing = curJob.GetTarget(TargetIndex.B).Thing as UnfinishedUpgrade;
                var thingToUpgrade = unfinishedThing.thingToUpgrade;
                List<Thing> ingredients = CalculateIngredients(curJob, actor);

                curJob.bill.Notify_IterationCompleted(actor, ingredients);

                var qComp = thingToUpgrade.TryGetComp<CompQuality>();
                if (qComp != null && qComp.Quality < QualityCategory.Legendary)
                {
                    qComp.SetQuality(qComp.Quality + 1, ArtGenerationContext.Colony);
                }

                var products = new List<Thing>
                {
                    thingToUpgrade
                };
                RecordsUtility.Notify_BillDone(actor, products);
                if (((curJob != null) ? curJob.bill : null) == null)
                {
                    for (int i = 0; i < products.Count; i++)
                    {
                        if (!GenPlace.TryPlaceThing(products[i], actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                        {
                            UpgradeQualityUtility.LogMessage(LogLevel.Error, actor.ToString(), "could not drop recipe product", products[i].ToString(), "near", actor.Position.ToString());
                        }
                    }
                    return;
                }
                if (curJob.bill.recipe.WorkAmountTotal((unfinishedThing != null) ? unfinishedThing.Stuff : null) >= 10000f && products.Count > 0)
                {
                    TaleRecorder.RecordTale(TaleDefOf.CompletedLongCraftingProject, new object[]
                    {
                        actor,
                        products[0].GetInnerIfMinified().def
                    });
                }
                if (products.Any<Thing>())
                {
                    Find.QuestManager.Notify_ThingsProduced(actor, products);
                }
                if (products.Count == 0)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
                    return;
                }
                if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
                {
                    for (int j = 0; j < products.Count; j++)
                    {
                        if (!GenPlace.TryPlaceThing(products[j], actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                        {
                            UpgradeQualityUtility.LogMessage(LogLevel.Error, actor.ToString(), "could not drop recipe product", products[j].ToString(), "near", actor.Position.ToString());
                        }
                    }
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
                    return;
                }
                if (products.Count > 1)
                {
                    for (int k = 1; k < products.Count; k++)
                    {
                        if (!GenPlace.TryPlaceThing(products[k], actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                        {
                            UpgradeQualityUtility.LogMessage(LogLevel.Error, actor.ToString(), "could not drop recipe product", products[k].ToString(), "near", actor.Position.ToString());
                        }
                    }
                }
                IntVec3 invalid = IntVec3.Invalid;
                if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellFor(products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out invalid, true);
                }
                else if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellForIn(products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetStoreZone().slotGroup, out invalid, true);
                }
                else
                {
                    Log.ErrorOnce("Unknown store mode", 9158246);
                }
                if (invalid.IsValid)
                {
                    actor.carryTracker.TryStartCarry(products[0]);
                    curJob.targetB = invalid;
                    if (productIndex != TargetIndex.None)
                    {
                        curJob.SetTarget(productIndex, products[0]);
                    }
                    curJob.count = 99999;
                    return;
                }
                if (!GenPlace.TryPlaceThing(products[0], actor.Position, actor.Map, ThingPlaceMode.Near, null, null, default(Rot4)))
                {
                    UpgradeQualityUtility.LogMessage(LogLevel.Error, "Bill doer could not drop product", products[0].ToString(), "near", actor.Position.ToString());
                }
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, true, true);
            };
            return toil;
        }
    }
}
