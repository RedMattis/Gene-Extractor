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

        private Graphic TopGraphic
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
        [Unsaved(false)] private Graphic cachedTopGraphic;


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
            if (base.Working)
            {
                if (selectedPawn != null)
                {
                    if (innerContainer.Contains(selectedPawn))
                    {
                        selectedPawn.Drawer.renderer.RenderPawnAt(DrawPos + PawnDrawOffset, null, neverAimWeapon: true);
                    }
                }
            }
            TopGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this);
        }

        protected static Dictionary<Rot4, ThingDef> GlowMotePerRotation;

        protected static Dictionary<Rot4, EffecterDef> BubbleEffecterPerRotation;


        // Operation
        protected void Cancel()
        {
            startTick = -1;
            selectedPawn = null;
            sustainerWorking = null;
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : Position, Map, ThingPlaceMode.Near);
        }

        protected void Fail()
        {
            if (innerContainer.Contains(selectedPawn))
            {
                innerContainer.TryDrop(selectedPawn, InteractionCell, base.Map, ThingPlaceMode.Near, 1, out var _);
                Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                selectedPawn.Kill(null, firstHediffOfDef);
            }
            OnStop();
        }

        private void OnStop()
        {
            selectedPawn = null;
            startTick = -1;
            sustainerWorking = null;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            sustainerWorking = null;
            ClearProgressBar();
            base.DeSpawn(mode);
        }

        protected void ClearProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        private void Finish()
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
                    Messages.Message("GET_Extracted".Translate(containedPawn.Name.ToStringShort, geneList.Join(x => x.LabelCap)), MessageTypeDefOf.TaskCompletion);
                }
            }
            TicksRemaining = ExtractionTimeInTicks;
            startTick = Find.TickManager.TicksGame;
        }


        // Pawn
        protected Pawn GetContainedPawn()
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
                int num = pawn.DeSpawnOrDeselect() ? 1 : 0;
                if (innerContainer.TryAddOrTransfer(pawn))
                {
                    startTick = Find.TickManager.TicksGame;
                    TicksRemaining = ExtractionTimeInTicks;
                }
                if (num != 0)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }


        // Float Menus
        public void OpenFloatMenuGenePicker()
        {
            var list = new List<FloatMenuOption>();
            var allPawnGenes = selectedPawn.genes.GenesListForReading.Select(x => x.def).ToList();
            if (GeneHelper.IsBaselinerOrEquavalent(allPawnGenes))
            {
                GeneHelper.AddBaselinerGenes(allPawnGenes);
            }

            foreach (var gene in allPawnGenes)
            {
                var existingGenes = GeneHelper.GetAllGenesOnMap(Map);
                if (existingGenes.ContainsKey(gene) && existingGenes[gene] == GeneState.SinglePack)
                {
                    continue;
                }

                list.Add(new FloatMenuOption(gene.LabelCap, delegate
                {
                    TargetGene = gene;
                    Log.Message($"DEBUG: Selected gene: \"{gene.label}\" for extraction.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }

        protected virtual void BuildFloatMenuAvailablePawns()
        {
            List<FloatMenuOption> list = [];
            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.genes != null)
                {
                    AcceptanceReport acceptanceReport = CanAcceptPawn(pawn);
                    string text = pawn.LabelShortCap + ", " + pawn.genes.XenotypeLabelCap;
                    if (!acceptanceReport.Accepted)
                    {
                        if (!acceptanceReport.Reason.NullOrEmpty())
                        {
                            list.Add(new FloatMenuOption(text + ": " + acceptanceReport.Reason, null, pawn, Color.white));
                        }
                    }
                    else
                    {
                        Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);
                        if (firstHediffOfDef != null)
                        {
                            text = text
                                + " ("
                                + firstHediffOfDef.LabelBase
                                + ", "
                                + firstHediffOfDef.TryGetComp<HediffComp_Disappears>()
                                    .ticksToDisappear
                                    .ToStringTicksToPeriod(allowSeconds: true, shortForm: true)
                                    .Colorize(ColoredText.SubtleGrayColor)
                                + ")";
                        }
                        list.Add(new FloatMenuOption(text, () => SelectPawn(pawn), pawn, Color.white));
                    }
                }
            }

            if (!list.Any())
            {
                list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }


        // Gizmos
        protected Command_Action BuildGizmoOverdrive()
        {
            Command_Action overdriveAction;

            if (OverchargeActive)
            {
                overdriveAction = new Command_Action
                {
                    defaultLabel = "GET_DeactivateOverdrive".Translate(),
                    defaultDesc = "GET_DeactivateOverdriveDesc".Translate(),
                    icon = Textures.CancelOverdrive,
                    action = delegate
                    {
                        OverchargeActive = false;
                        TicksRemaining *= OverchargeSpeedFactor;
                    }
                };
            }
            else
            {
                overdriveAction = new Command_Action
                {
                    defaultLabel = "GET_ActivateOverdrive".Translate(),
                    defaultDesc = "GET_ActivateOverdriveDesc".Translate(),
                    icon = Textures.ActivateOverdrive,
                    action = delegate
                    {
                        OverchargeActive = true;
                        TicksRemaining /= OverchargeSpeedFactor;
                    }
                };
            }

            return overdriveAction;
        }

        protected Command_Action BuildGizmoSelectGene()
        {
            return new()
            {
                defaultLabel = "GET_SelectGene".Translate(),
                defaultDesc = "GET_SelectGeneDesc".Translate(),
                icon = Textures.TargetGeneIcon, // FIX ICON.
                action = OpenFloatMenuGenePicker
            };
        }

        protected Command_Action BuildGizmoCancelExtraction()
        {
            return new()
            {
                defaultLabel = "CommandCancelExtraction".Translate(),
                defaultDesc = "CommandCancelExtractionDesc".Translate(),
                icon = Textures.CancelLoadingIcon,
                activateSound = SoundDefOf.Designate_Cancel,
                action = Cancel
            };
        }

        protected Command_Action BuildGizmoCancelLoad()
        {
            return new()
            {
                defaultLabel = "CommandCancelLoad".Translate(),
                defaultDesc = "CommandCancelLoadDesc".Translate(),
                icon = Textures.CancelIcon,
                activateSound = SoundDefOf.Designate_Cancel,
                action = delegate
                {
                    innerContainer.TryDropAll(Position, base.Map, ThingPlaceMode.Near);
                    if (selectedPawn.CurJobDef == JobDefOf.EnterBuilding)
                    {
                        selectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                    selectedPawn = null;
                    startTick = -1;
                    sustainerWorking = null;
                }
            };
        }

        protected Command_Action BuildGizmoInsertPawn()
        {
            var insertPerson = new Command_Action()
            {
                defaultLabel = "InsertPerson".Translate() + "...",
                defaultDesc = "InsertPersonGeneExtractorDesc".Translate(),
                icon = Textures.InsertPawn,
                action = BuildFloatMenuAvailablePawns,
            };

            if (!PowerOn)
            {
                insertPerson.Disable("NoPower".Translate().CapitalizeFirst());
            }

            return insertPerson;
        }

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

            yield return BuildGizmoOverdrive();

            if (base.Working)
            {
                // Add dropdown with all genes available on the pawn.
                if (CanTargetExtraction || Settings.allVatsCanTargetGenes)
                {
                    yield return BuildGizmoSelectGene();
                }

                yield return BuildGizmoCancelExtraction();

                if (DebugSettings.ShowDevGizmos)
                {
                    foreach (var gizmo in BuildGizmosDevGizmos())
                    {
                        yield return gizmo;
                    }
                }

                yield break;
            }

            if (selectedPawn != null)
            {
                yield return BuildGizmoCancelLoad();
                yield break;
            }

            yield return BuildGizmoInsertPawn();
        }


        // Inspect string buildout
        protected void InspectStringAddTime(StringBuilder stringBuilder)
        {
            stringBuilder
                .AppendLineIfNotEmpty()
                .Append($"{"TimeLeft".Translate().CapitalizeFirst()}: {(TicksRemaining / 2500) + 1} {"HoursLower".Translate()}");
        }

        protected void InspectStringAddPawn(StringBuilder stringBuilder)
        {
            stringBuilder
                .AppendLineIfNotEmpty()
                .Append($"{"CasketContains".Translate()}: {selectedPawn.NameShortColored.Resolve()}, {selectedPawn.ageTracker.AgeBiologicalYears}");
        }

        protected abstract void InspectStringAddResourceStarvation(StringBuilder stringBuilder);

        protected abstract void InspectStringAddResourceConsumption(StringBuilder stringBuilder);

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());

            if (base.Working)
            {
                if (selectedPawn != null && innerContainer.Contains(selectedPawn))
                {
                    InspectStringAddTime(stringBuilder);
                    InspectStringAddPawn(stringBuilder);
                }

                InspectStringAddResourceStarvation(stringBuilder);
            }
            else if (selectedPawn != null)
            {
                stringBuilder.AppendLineIfNotEmpty().Append("WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve());
            }

            InspectStringAddResourceConsumption(stringBuilder);

            if (base.Working)
            {
                if (TargetGene != null)
                {
                    stringBuilder.AppendLineIfNotEmpty().Append("GET_TargetGene".Translate(TargetGene.LabelCap));
                }
            }

            return stringBuilder.ToString();
        }


        // Tick
        protected abstract bool Tick_ResourceStarvation();

        protected void TickHandleSustainer()
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
                TickEffects();
                if (PowerOn) TicksRemaining--;

                if (TicksRemaining <= 0) Finish();
            }
            else if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        private void TickEffects()
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
