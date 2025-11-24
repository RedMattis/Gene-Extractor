using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeneExtractorTiers.Utility;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace GeneExtractorTiers.Extractors
{
    [StaticConstructorOnStartup]
    public abstract class GeneExtractorBase : Building_Enterable, IThingHolderWithDrawnPawn, IThingHolder
    {
        #region IThingHolderWithDrawnPawn Implementation
        public float HeldPawnDrawPos_Y => DrawPos.y + 3f / 74f;

        public float HeldPawnBodyAngle => base.Rotation.AsAngle; //0;

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        #endregion

        public virtual bool TargetSelected => selectedPawn != null;

        public virtual bool ContainsTarget => innerContainer.Contains(selectedPawn);

        protected const float WorkingPowerUsageFactor = 1f;

        protected const float OverchargePowerFactor = 4f;

        protected const int OverchargeSpeedFactor = 2;


        // Settings
        private static ExtractorTierSettings _settings = null;
        public static ExtractorTierSettings Settings => _settings ??= LoadedModManager.GetMod<GeneExtractorMain>().GetSettings<ExtractorTierSettings>();


        public virtual bool CanExtractArchite => false;

        public virtual bool CanTargetExtraction => false;

        public GeneDef TargetGene = null;

        public virtual float SpeedMultiplier => 1;

        protected bool OverchargeActive = false;

        public virtual int ExtractionTimeInTicks => (int)(Settings.extractionHours * 2500 / SpeedMultiplier) / (OverchargeActive ? OverchargeSpeedFactor : 1);


        // Work
        protected int TicksRemaining = 0;
        protected int ProgressBarTicks = 0;


        // Graphics
        private const float ProgressBarOffsetZ = -0.82f;

        protected virtual Graphic TopGraphic
        {
            get
            {
                if (cachedTopGraphic == null)
                {
                    cachedTopGraphic = GraphicDatabase.Get<Graphic_Multi>("GET_ExtractorTop", ShaderDatabase.Transparent, def.graphicData.drawSize, Color.white);
                }
                return cachedTopGraphic;
            }
        }


        // Unsaved
        [Unsaved(false)] private CompPowerTrader cachedPowerComp;
        [Unsaved(false)] private Sustainer sustainerWorking;
        [Unsaved(false)] private Effecter progressBar;
        [Unsaved(false)] private Effecter bubbleEffecter;
        [Unsaved(false)] protected Graphic cachedTopGraphic;

        protected void StopSustainer() => sustainerWorking = null;

        protected void ResetStartTick() => startTick = -1;

        protected void SetStartTick() => startTick = Find.TickManager.TicksGame;

        protected virtual void UnsetTarget() => selectedPawn = null;



        // State
        protected bool PowerOn => PowerTraderComp.PowerOn;

        protected virtual CompPowerTrader PowerTraderComp => cachedPowerComp ??= this.TryGetComp<CompPowerTrader>();


        // Save Game persistence
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref TicksRemaining, "TicksRemaining", 0);
            Scribe_Values.Look(ref OverchargeActive, "overchargeActive", false);
            Scribe_Defs.Look(ref TargetGene, "targetGene");
        }

        // Graphics (Again)
        //NOTE: Why do the pawns not float like the Biosculptor?
        public override Vector3 PawnDrawOffset => CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (Working)
            {
                if (TargetSelected)
                {
                    DrawPawn();
                }
            }
            TopGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this);
        }

        protected virtual void DrawPawn()
        {
            if (ContainsTarget)
            {
                selectedPawn.Drawer.renderer.RenderPawnAt(DrawPos + PawnDrawOffset, null, neverAimWeapon: true);
            }
        }

        protected static Dictionary<Rot4, ThingDef> GlowMotePerRotation;

        protected static Dictionary<Rot4, EffecterDef> BubbleEffecterPerRotation;


        // Operation
        protected virtual void Cancel()
        {
            OnStop();
        }

        protected virtual void DropContents()
        {
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : Position, Map, ThingPlaceMode.Near);
        }

        protected virtual void Fail()
        {
            if (ContainsTarget)
            {
                innerContainer.TryDrop(selectedPawn, InteractionCell, base.Map, ThingPlaceMode.Near, 1, out var _);
                KillPawnFromStarvation();
            }
            OnStop();
        }

        protected virtual void KillPawnFromStarvation()
        {
            Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
            selectedPawn.Kill(null, firstHediffOfDef);
        }

        protected virtual void OnStop()
        {
            DropContents();
            UnsetTarget();
            ResetStartTick();
            StopSustainer();
            ClearProgressBar();
        }

        protected virtual void StartNewCycle()
        {
            TicksRemaining = ExtractionTimeInTicks;
            SetStartTick();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            OnStop();
            base.DeSpawn(mode);
        }

        protected virtual void ClearProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        protected virtual void Finish()
        {
            if (GetContainedPawn() != null)
            {
                Pawn containedPawn = GetContainedPawn();

                var existingGenes = GeneHelper.GetAllGenesOnMap(Map);
                var validPawnGenes = containedPawn.genes.GenesListForReading.Where(x => x.def.biostatArc == 0 || CanExtractArchite).Select(x => x.def).ToList();

                validPawnGenes.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);

                // Check if the gene-category is "BS_DO_NOT"
                validPawnGenes = validPawnGenes.Where(x => !x.displayCategory.defName.Contains("BS_DO_NOT")).ToList();

                var pickableGenes = validPawnGenes.OrderBy(x => Rand.Range(0, 1f)).ToList();

                // Check if baseliner
                if (GeneHelper.IsBaselinerOrEquavalent(pickableGenes))
                {
                    GeneHelper.AddBaselinerGenes(pickableGenes);
                }

                var newGenes = pickableGenes.Where(x => !existingGenes.ContainsKey(x)).ToList();
                var almostNewGenes = pickableGenes.Where(x => !existingGenes.ContainsKey(x) || (existingGenes.ContainsKey(x) && existingGenes[x] == GeneState.Multipack)).ToList();
                var pickableNewish = newGenes.Concat(almostNewGenes).ToHashSet().OrderBy(x => Rand.Range(0, 1f)).ToList();

                List<GeneDef> genesInPack = GeneHelper.BuildGeneListFromPawn(containedPawn, ref TargetGene,
                    pickableGenes, pickableNewish, Settings.megaMultipackChance, Settings.multipackChance);

                var genesInPackListOfLists = new List<List<GeneDef>>();
                if (Rand.Chance(Settings.splitZeroCost))
                {
                    // Create two packs, one with zero cost genes and one with the rest.
                    var zeroCostGenes = genesInPack.Where(x => x.biostatArc == 0 && x.biostatMet == 0 && x.biostatCpx <= 1).ToList();
                    if (zeroCostGenes.Any())
                    {
                        genesInPackListOfLists.Add(zeroCostGenes);
                    }
                    var nonZeroCostGenes = genesInPack.Where(x => !zeroCostGenes.Contains(x));
                    if (nonZeroCostGenes.Any())
                    {
                        genesInPackListOfLists.Add(nonZeroCostGenes.ToList());
                    }
                    if (zeroCostGenes.Any() && nonZeroCostGenes.Any())
                    {
                        Messages.Message("GET_DidSplitZeroCost".Translate(), MessageTypeDefOf.TaskCompletion);
                    }
                }
                else
                {
                    genesInPackListOfLists.Add(genesInPack);
                }

                foreach (var geneList in genesInPackListOfLists)
                {
                    Genepack genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
                    genepack.Initialize(geneList);
                    IntVec3 center = (def.hasInteractionCell ? InteractionCell : base.Position);
                    GenPlace.TryPlaceThing(genepack, center, Map, ThingPlaceMode.Near);

                    SetPawnHediffXenogermReplicating(containedPawn);
                    Messages.Message("GET_Extracted".Translate(containedPawn.Name.ToStringShort, geneList.Join(x => x.LabelCap)), MessageTypeDefOf.TaskCompletion);
                }
            }
            StartNewCycle();
        }

        protected virtual void SetPawnHediffXenogermReplicating(Pawn containedPawn)
        {
            if (Settings.RegrowTimeInTicks > 0)
            {
                Hediff hediff = containedPawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);
                if (hediff == null)
                {
                    hediff = HediffMaker.MakeHediff(HediffDefOf.XenogermReplicating, containedPawn);
                    containedPawn.health.AddHediff(hediff);
                }
                hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = Settings.RegrowTimeInTicks;
            }
        }

        protected virtual void CancelLoad()
        {
            CancelEnterBuilding();
            OnStop();
        }

        protected virtual void CancelEnterBuilding()
        {
            if (selectedPawn.CurJobDef == JobDefOf.EnterBuilding)
            {
                selectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        protected virtual void ActivateOverdrive()
        {
            OverchargeActive = true;
            TicksRemaining /= OverchargeSpeedFactor;
        }

        protected virtual void DeactivateOverdrive()
        {
            OverchargeActive = false;
            TicksRemaining *= OverchargeSpeedFactor;
        }

        protected virtual void SetTargetGene(GeneDef gene)
        {
            TargetGene = gene;
            Log.Message($"DEBUG: Selected gene: \"{gene.label}\" for extraction.");
        }


        // Pawn
        protected virtual Pawn GetContainedPawn()
        {
            if (!innerContainer.Any(x => x is Pawn))
            {
                return null;
            }
            return (Pawn)innerContainer.Where(x => x is Pawn).First();
        }

        public override void TryAcceptPawn(Pawn pawn)
        {
            if ((bool)CanAcceptPawn(pawn))
            {
                selectedPawn = pawn;
                bool deselect = pawn.DeSpawnOrDeselect();

                if (innerContainer.TryAddOrTransfer(pawn))
                {
                    SetStartTick();
                    TicksRemaining = ExtractionTimeInTicks;
                }
                if (deselect)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }


        // Float Menus
        protected virtual void OpenFloatMenuGenePicker()
        {
            FloatMenuHelper.OpenFloatMenuGenePicker(GetContainedPawn(), Map, SetTargetGene);
        }

        protected virtual void BuildFloatMenuAvailablePawns()
        {
            FloatMenuHelper.BuildFloatMenuAvailablePawns(Map, CanAcceptPawn, SelectPawn);
        }


        // Gizmos
        protected virtual IEnumerable<Gizmo> BuildGizmosDevGizmos()
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Finish extraction",
                action = Finish
            };
        }

        protected virtual IEnumerable<Gizmo> BuildGizmosSettings()
        {
            return [];
        }

        protected virtual Gizmo BuildInsertGizmo()
            => GizmoHelper.BuildGizmoInsertPawn(BuildFloatMenuAvailablePawns, PowerOn);

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            //Settings Copy/Paste
            foreach (Gizmo gizmo in BuildGizmosSettings())
            {
                yield return gizmo;
            }

            yield return GizmoHelper.BuildGizmoOverdrive(OverchargeActive, ActivateOverdrive, DeactivateOverdrive);

            if (Working)
            {
                // Add dropdown with all genes available on the pawn.
                if (CanTargetExtraction || Settings.allVatsCanTargetGenes)
                {
                    yield return GizmoHelper.BuildGizmoSelectGene(OpenFloatMenuGenePicker);
                }

                yield return GizmoHelper.BuildGizmoCancelExtraction(Cancel);

                if (DebugSettings.ShowDevGizmos)
                {
                    foreach (var gizmo in BuildGizmosDevGizmos())
                    {
                        yield return gizmo;
                    }
                }

                yield break;
            }

            if (TargetSelected)
            {
                yield return GizmoHelper.BuildGizmoCancelLoad(CancelLoad);
                yield break;
            }

            yield return BuildInsertGizmo();
        }


        // Inspect string build-out
        protected virtual void InspectStringAddTime(StringBuilder stringBuilder)
        {
            stringBuilder
                .AppendLineIfNotEmpty()
                .Append($"{"TimeLeft".Translate().CapitalizeFirst()}: {(TicksRemaining / 2500) + 1} {"HoursLower".Translate()}");
        }

        protected virtual void InspectStringAddPawn(StringBuilder stringBuilder)
        {
            stringBuilder
                .AppendLineIfNotEmpty()
                .Append($"{"CasketContains".Translate()}: {GetContainedNameColorized()}, {GetContainedAge()}");
        }

        protected abstract void InspectStringAddResourceStarvation(StringBuilder stringBuilder);

        protected abstract void InspectStringAddResourceConsumption(StringBuilder stringBuilder);

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());

            if (Working)
            {
                if (TargetSelected && ContainsTarget)
                {
                    InspectStringAddTime(stringBuilder);
                    InspectStringAddPawn(stringBuilder);
                }

                InspectStringAddResourceStarvation(stringBuilder);
            }
            else if (TargetSelected)
            {
                stringBuilder
                    .AppendLineIfNotEmpty()
                    .Append("WaitingForPawn".Translate(GetTargetName()).Resolve());
            }

            InspectStringAddResourceConsumption(stringBuilder);

            if (Working)
            {
                if (TargetGene != null)
                {
                    stringBuilder.AppendLineIfNotEmpty().Append("GET_TargetGene".Translate(TargetGene.LabelCap));
                }
            }

            return stringBuilder.ToString();
        }

        protected virtual NamedArgument GetTargetName()
        {
            return selectedPawn.Named("PAWN");
        }

        protected virtual string GetContainedNameColorized() => selectedPawn.NameShortColored.Resolve();

        protected virtual int GetContainedAge() => selectedPawn.ageTracker.AgeBiologicalYears;



        // Tick
        protected override void Tick()
        {
            base.Tick();
            //innerContainer.DoTick();

            if (Working)
            {
                if (Tick_ResourceStarvation())
                    return;

                Tick_HandleSustainer();
                Tick_ConsumeResources();
                Tick_GlowMote();
            }

            Tick_ConsumePower();
            Tick_DoWork();
        }

        protected abstract bool Tick_ResourceStarvation();

        protected void Tick_HandleSustainer()
        {
            if (sustainerWorking == null || sustainerWorking.Ended)
            {
                sustainerWorking = SoundDefOf.GrowthVat_Working.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            }
            else
            {
                sustainerWorking.Maintain();
            }
        }

        protected abstract void Tick_ConsumeResources();

        protected void Tick_GlowMote()
        {
            if (GlowMotePerRotation == null)
            {
                GlowMotePerRotation = new Dictionary<Rot4, ThingDef>
                {
                    {
                        Rot4.South,
                        ThingDefOf.Mote_VatGlowVertical
                    },
                    {
                        Rot4.East,
                        ThingDefOf.Mote_VatGlowHorizontal
                    },
                    {
                        Rot4.West,
                        ThingDefOf.Mote_VatGlowHorizontal
                    },
                    {
                        Rot4.North,
                        ThingDefOf.Mote_VatGlowVertical
                    },
                };

                BubbleEffecterPerRotation = new Dictionary<Rot4, EffecterDef>
                {
                    {
                        Rot4.South,
                        EffecterDefOf.Vat_Bubbles_South
                    },
                    {
                        Rot4.East,
                        EffecterDefOf.Vat_Bubbles_East
                    },
                    {
                        Rot4.West,
                        EffecterDefOf.Vat_Bubbles_West
                    },
                    {
                        Rot4.North,
                        EffecterDefOf.Vat_Bubbles_North
                    },
                };
            }

            if (this.IsHashIntervalTick(132))
            {
                MoteMaker.MakeStaticMote(DrawPos, base.MapHeld, GlowMotePerRotation[base.Rotation]);
            }

            if (bubbleEffecter == null)
            {
                bubbleEffecter = BubbleEffecterPerRotation[base.Rotation].SpawnAttached(this, base.MapHeld);
            }
            bubbleEffecter.EffectTick(this, this);
        }

        protected void Tick_ConsumePower()
        {
            if (this.IsHashIntervalTick(250))
            {
                var num = Working ? WorkingPowerUsageFactor : 1f;
                num *= OverchargeActive ? OverchargePowerFactor : 1f;
                PowerTraderComp.PowerOutput = (0f - PowerComp.Props.PowerConsumption) * num;
            }
        }

        protected virtual void Tick_DoWork()
        {
            if (Working && PowerTraderComp.PowerOn)
            {
                Tick_Effects();
                if (PowerOn) TicksRemaining--;

                if (TicksRemaining <= 0) Finish();
            }
            else if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        protected virtual void Tick_Effects()
        {
            if (sustainerWorking == null || sustainerWorking.Ended)
                sustainerWorking =
                    SoundDefOf.GeneExtractor_Working.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            else
                sustainerWorking.Maintain();

            // For whatever reason the progress bar yeets itself after awhile, so we'll just recreate it every 100000 ticks
            if (ProgressBarTicks > 10000)
            {
                ClearProgressBar();
                ProgressBarTicks = 0;
            }

            progressBar ??= EffecterDefOf.ProgressBarAlwaysVisible.Spawn();

            progressBar.EffectTick(new TargetInfo(Position + IntVec3.North.RotatedBy(Rotation), Map), TargetInfo.Invalid);
            var mote = ((SubEffecter_ProgressBar)progressBar.children[0]).mote;
            mote.progress = 1f - ((float)TicksRemaining / ExtractionTimeInTicks);
            mote.offsetZ = ProgressBarOffsetZ;
            mote.solidTimeOverride = ExtractionTimeInTicks;
            ProgressBarTicks++;
            //if (mote != null)
            //{

            //}
        }
    }
}
