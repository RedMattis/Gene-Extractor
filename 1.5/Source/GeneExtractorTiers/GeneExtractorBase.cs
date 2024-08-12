using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using Verse.Sound;
using Verse.AI;

namespace GeneExtractorTiers
{

    [StaticConstructorOnStartup]
    public abstract class Building_GeneExtractorTier : Building_Enterable, IStoreSettingsParent, IThingHolderWithDrawnPawn, IThingHolder
    {
        public virtual bool CanExtractArchite => false;
        public virtual bool CanTargetExtraction => false;
        public GeneDef targetGene = null;

        // Settings
        private static ExtractorTierSettings _settings = null;
        public static ExtractorTierSettings Settings => _settings ??= LoadedModManager.GetMod<GeneExtractorMain>().GetSettings<ExtractorTierSettings>();

        // Properties
        public virtual float SpeedMultiplier => 1;
        public int ExtractionTimeInTicks => (int)(Settings.extractionHours * 2500 / SpeedMultiplier) / (overchargeActive ? OverchargeSpeedFactor : 1 );

        private const float WorkingPowerUsageFactor = 1f;
        private const float BasePawnConsumedNutritionPerDay = 3f;
        private const float BiostarvationGainPerDayNoFoodOrPower = 0.5f;
        private const float BiostarvationFallPerDayPoweredAndFed = 0.1f;
        public const float NutritionBuffer = 10f;
        private StorageSettings allowedNutritionSettings;

        private bool overchargeActive = false;
        private const float OverchargePowerFactor = 4f;
        private const float OverchargeNutritionFactor = 3.0f;
        private const int OverchargeSpeedFactor = 2;

        // Unsaved
        [Unsaved(false)] private CompPowerTrader? cachedPowerComp;
        [Unsaved(false)] private Sustainer? sustainerWorking;
        [Unsaved(false)] private Effecter? progressBar;
        [Unsaved(false)] private Texture2D? cachedInsertPawnTex;
        [Unsaved(false)] private Effecter bubbleEffecter;
        [Unsaved(false)] private Graphic cachedTopGraphic;

        // State
        public bool PowerOn => PowerTraderComp.PowerOn;
        private CompPowerTrader PowerTraderComp => cachedPowerComp ??= this.TryGetComp<CompPowerTrader>();
        private int ticksRemaining = 0;
        private float containedNutrition;

        // UI
        private static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public static readonly CachedTexture InsertPawnIcon = new CachedTexture("UI/Gizmos/InsertPawn");
        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        private static readonly Texture2D ActivateOverdrive = ContentFinder<Texture2D>.Get("GET_OverdriveOn");
        private static readonly Texture2D CancelOverdrive = ContentFinder<Texture2D>.Get("GET_OverdriveOff");
        private static readonly Texture2D TargetGeneIcon = ContentFinder<Texture2D>.Get("GET_TargetGene");

        // Graphics
        public float HeldPawnDrawPos_Y => DrawPos.y + 3f / 74f;
        public float HeldPawnBodyAngle => base.Rotation.AsAngle; //0;
        //public override Vector3 PawnDrawOffset => Vector3.zero;
        public override Vector3 PawnDrawOffset => CompBiosculpterPod.FloatingOffset(Find.TickManager.TicksGame);
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        private const float ProgressBarOffsetZ = -0.82f;
        public Texture2D InsertPawnTex
        {
            get
            {
                if (cachedInsertPawnTex == null)
                {
                    cachedInsertPawnTex = ContentFinder<Texture2D>.Get("UI/Gizmos/InsertPawn");
                }
                return cachedInsertPawnTex;
            }
        }

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
            TopGraphic.Draw(DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this); // 
        }

        public float BiostarvationDailyOffset
        {
            get
            {
                if (!base.Working)
                {
                    return 0f;
                }
                if (!PowerOn || containedNutrition <= 0f)
                {
                    return 0.5f;
                }
                return -0.1f;
            }
        }

        public float NutritionStored
        {
            get
            {
                float num = containedNutrition;
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    Thing thing = innerContainer[i];
                    num += (float)thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
                }
                return num;
            }
        }

        public float NutritionNeeded
        {
            get
            {
                if (selectedPawn == null)
                {
                    return 0f;
                }
                return 10f - NutritionStored;
            }
        }

        private void TryAbsorbNutritiousThing()
        {
            for (int i = 0; i < innerContainer.Count; i++)
            {
                if (innerContainer[i] != selectedPawn && innerContainer[i].def != ThingDefOf.Xenogerm)
                {
                    float statValue = innerContainer[i].GetStatValue(StatDefOf.Nutrition);
                    if (statValue > 0f)
                    {
                        containedNutrition += statValue;
                        innerContainer[i].SplitOff(1).Destroy();
                        break;
                    }
                }
            }
        }

        private float BiostarvationSeverityPercent
        {
            get
            {
                if (selectedPawn != null)
                {
                    Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                    if (firstHediffOfDef != null)
                    {
                        return firstHediffOfDef.Severity / HediffDefOf.BioStarvation.maxSeverity;
                    }
                }
                return 0f;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }
            AcceptanceReport acceptanceReport = CanAcceptPawn(selPawn);
            if (acceptanceReport.Accepted)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
                {
                    SelectPawn(selPawn);
                }), selPawn, this);
            }
            else if (SelectedPawn == selPawn && !selPawn.IsPrisonerOfColony)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
                {
                    selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.EnterBuilding, this), JobTag.Misc);
                }), selPawn, this);
            }
            else if (!acceptanceReport.Reason.NullOrEmpty())
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + acceptanceReport.Reason.CapitalizeFirst(), null);
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            allowedNutritionSettings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
            {
                allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
            }
        }

        private void Cancel()
        {
            startTick = -1;
            selectedPawn = null;
            sustainerWorking = null;
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : Position, Map, ThingPlaceMode.Near);
        }

        public void OpenFloatMenuGenePicker()
        {
            var list = new List<FloatMenuOption>();
            var allPawnGenes = selectedPawn.genes.GenesListForReading.Select(x => x.def).ToList();
            if (IsBaselinerOrEquavalent(allPawnGenes))
            {
                AddBaselinerGenes(allPawnGenes);
            }

            foreach (var gene in allPawnGenes)
            {
                var existingGenes = GetAllGenesOnCurrentMap();
                if (existingGenes.ContainsKey(gene) && existingGenes[gene] == GeneState.SinglePack)
                {
                    continue;
                }

                list.Add(new FloatMenuOption(gene.LabelCap, delegate
                {
                    targetGene = gene;
                    Log.Message($"DEBUG: Selected gene: \"{gene.label}\" for extraction.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
            {
                yield return item;
            }
            Command_Action overdriveAction;
            if (overchargeActive)
            {
                overdriveAction = new Command_Action
                {
                    defaultLabel = "GET_DeactivateOverdrive".Translate(),
                    defaultDesc = "GET_DeactivateOverdriveDesc".Translate(),
                    icon = CancelOverdrive,
                    action = delegate
                    {
                        overchargeActive = false;
                        ticksRemaining *= OverchargeSpeedFactor;
                    }
                };
            }
            else
            {
                overdriveAction = new Command_Action
                {
                    defaultLabel = "GET_ActivateOverdrive".Translate(),
                    defaultDesc = "GET_ActivateOverdriveDesc".Translate(),
                    icon = ActivateOverdrive,
                    action = delegate
                    {
                        overchargeActive = true;
                        ticksRemaining /= OverchargeSpeedFactor;
                    }
                };
            }
            yield return overdriveAction;
            if (base.Working)
            {
                // Add dropdown with all genes available on the pawn.
                if (CanTargetExtraction || Settings.allVatsCanTargetGenes)
                {
                    Command_Action targetExctractionAction = new Command_Action
                    {
                        defaultLabel = "GET_SelectGene".Translate(),
                        defaultDesc = "GET_SelectGeneDesc".Translate(),
                        icon = TargetGeneIcon, // FIX ICON.
                        action = OpenFloatMenuGenePicker
                    };
                    yield return targetExctractionAction;
                }

                Command_Action command_Action = new Command_Action
                {
                    defaultLabel = "CommandCancelExtraction".Translate(),
                    defaultDesc = "CommandCancelExtractionDesc".Translate(),
                    icon = CancelLoadingIcon,
                    activateSound = SoundDefOf.Designate_Cancel,
                    action = Cancel
                };
                yield return command_Action;

                

                if (DebugSettings.ShowDevGizmos)
                {
                    Command_Action command_Action2 = new Command_Action
                    {
                        defaultLabel = "DEV: Finish extraction",
                        action = Finish
                    };
                    yield return command_Action2;

                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Fill nutrition",
                        action = delegate
                        {
                            containedNutrition = 10f;
                        }
                    };
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Empty nutrition",
                        action = delegate
                        {
                            containedNutrition = 0f;
                        }
                    };
                }
                yield break;
            }
            if (selectedPawn != null)
            {
                Command_Action command_Action3 = new()
                {
                    defaultLabel = "CommandCancelLoad".Translate(),
                    defaultDesc = "CommandCancelLoadDesc".Translate(),
                    icon = CancelIcon,
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
                yield return command_Action3;
                yield break;
            }
            Command_Action command_Action4 = new()
            {
                defaultLabel = "InsertPerson".Translate() + "...",
                defaultDesc = "InsertPersonGeneExtractorDesc".Translate(),
                icon = InsertPawnTex,
                action = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    foreach (Pawn item in Map.mapPawns.AllPawnsSpawned)
                    {
                        Pawn pawn = item;
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
                                    text = text + " (" + firstHediffOfDef.LabelBase + ", " + firstHediffOfDef.TryGetComp<HediffComp_Disappears>().ticksToDisappear.ToStringTicksToPeriod(allowSeconds: true, shortForm: true).Colorize(ColoredText.SubtleGrayColor) + ")";
                                }
                                list.Add(new FloatMenuOption(text, delegate
                                {
                                    SelectPawn(pawn);
                                }, pawn, Color.white));
                            }
                        }
                    }
                    if (!list.Any())
                    {
                        list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
            if (!PowerOn)
            {
                command_Action4.Disable("NoPower".Translate().CapitalizeFirst());
            }
            yield return command_Action4;
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (base.Working)
            {
                if (selectedPawn != null && innerContainer.Contains(selectedPawn))
                {
                    stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1} {2}", "TimeLeft".Translate().CapitalizeFirst().ToString(), (ticksRemaining / 2500) + 1, "HoursLower".Translate().ToString()));
                    stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1}, {2}", "CasketContains".Translate().ToString(), selectedPawn.NameShortColored.Resolve(), selectedPawn.ageTracker.AgeBiologicalYears));
                }
                float biostarvationSeverityPercent = BiostarvationSeverityPercent;
                if (biostarvationSeverityPercent > 0f)
                {
                    string text = ((BiostarvationDailyOffset >= 0f) ? "+" : string.Empty);
                    stringBuilder.AppendLineIfNotEmpty().Append(string.Format("{0}: {1} ({2})", "Biostarvation".Translate(), biostarvationSeverityPercent.ToStringPercent(), "PerDay".Translate(text + BiostarvationDailyOffset.ToStringPercent())));
                }
            }
            else if (selectedPawn != null)
            {
                stringBuilder.AppendLineIfNotEmpty().Append("WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve());
            }
            stringBuilder.AppendLineIfNotEmpty().Append("Nutrition".Translate()).Append(": ")
                .Append(NutritionStored.ToStringByStyle(ToStringStyle.FloatMaxOne));
            if (base.Working)
            {
                stringBuilder.Append(" (-").Append("PerDay".Translate((NutritionConsumedPerDay * Settings.nutritionMultiplier).ToString("F1"))).Append(")");

                if (targetGene != null)
                {
                    stringBuilder.AppendLineIfNotEmpty().Append("GET_TargetGene".Translate(targetGene.LabelCap));
                }
            }
            return stringBuilder.ToString();
        }

        private Pawn GetContainedPawn()
        {
            if (!innerContainer.Any(x=>x is Pawn))
            {
                return null;
            }
            return (Pawn)innerContainer.Where(x=>x is Pawn).First();
        }

        public override AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            if (!pawn.IsColonist && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony && (!pawn.IsColonyMutant || !pawn.IsGhoul))
            {
                return false;
            }
            if (selectedPawn != null && selectedPawn != pawn)
            {
                return false;
            }
            if (!pawn.RaceProps.Humanlike || pawn.IsQuestLodger())
            {
                return false;
            }
            if (!PowerOn)
            {
                return "NoPower".Translate().CapitalizeFirst();
            }
            if (innerContainer.Any(x=>x is Pawn))
            {
                return "Occupied".Translate();
            }
            // Special behaviour for Baseliners!
            //if (pawn.genes == null || !pawn.genes.GenesListForReading.Any((Gene x) => x.def.passOnDirectly))
            //{
            //    return "PawnHasNoGenes".Translate(pawn.Named("PAWN"));
            //}
            if (pawn?.genes?.GenesListForReading?.Any(x => x.def.defName == "VREA_Power") == true)
            {
                return "VREA.CannotUseAndroid".Translate().CapitalizeFirst();
            }
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.XenogerminationComa))
            {
                return "InXenogerminationComa".Translate();
            }
            return true;
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
                    ticksRemaining = ExtractionTimeInTicks;
                }
                if (num != 0)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }

        public float NutritionConsumedPerDay
        {
            get
            {
                float num = 3f;
                if (BiostarvationSeverityPercent > 0f)
                {
                    float num2 = 1.1f;
                    num *= num2;
                }
                if (overchargeActive)
                {
                    num *= OverchargeNutritionFactor;
                }
                return num;
            }
        }

        public bool StorageTabVisible => true;

        private static Dictionary<Rot4, ThingDef> GlowMotePerRotation;

        private static Dictionary<Rot4, EffecterDef> BubbleEffecterPerRotation;

        public override void Tick()
        {
            base.Tick();
            innerContainer.ThingOwnerTick();

            if (base.Working)
            {
                if (selectedPawn != null)
                {
                    float num = BiostarvationDailyOffset / 60000f * HediffDefOf.BioStarvation.maxSeverity;
                    Hediff firstHediffOfDef = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
                    if (firstHediffOfDef != null)
                    {
                        firstHediffOfDef.Severity += num;
                        if (firstHediffOfDef.ShouldRemove)
                        {
                            selectedPawn.health.RemoveHediff(firstHediffOfDef);
                        }
                    }
                    else if (num > 0f)
                    {
                        Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BioStarvation, selectedPawn);
                        hediff.Severity = num;
                        selectedPawn.health.AddHediff(hediff);
                    }
                }
                if (BiostarvationSeverityPercent >= 1f)
                {
                    Fail();
                    return;
                }
                if (sustainerWorking == null || sustainerWorking.Ended)
                {
                    sustainerWorking = SoundDefOf.GrowthVat_Working.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                }
                else
                {
                    sustainerWorking.Maintain();
                }
                containedNutrition = Mathf.Clamp(containedNutrition - NutritionConsumedPerDay * Settings.nutritionMultiplier / 60000f, 0f, 2.1474836E+09f);
                if (containedNutrition <= 0f)
                {
                    TryAbsorbNutritiousThing();
                }
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
                        }
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
                        }
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

            if (this.IsHashIntervalTick(250))
            {
                var num = Working ? WorkingPowerUsageFactor : 1f;
                num *= overchargeActive ? OverchargePowerFactor : 1f;
                PowerTraderComp.PowerOutput = (0f - PowerComp.Props.PowerConsumption) * num;
            }

            if (Working && PowerTraderComp.PowerOn)
            {
                TickEffects();
                if (PowerOn) ticksRemaining--;

                if (ticksRemaining <= 0) Finish();
            }
            else if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        private void Fail()
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

        private void ClearProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        int progressBarTicks = 0;
        private void TickEffects()
        {
            if (sustainerWorking == null || sustainerWorking.Ended)
                sustainerWorking =
                    SoundDefOf.GeneExtractor_Working.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            else
                sustainerWorking.Maintain();

            // For whatever reason the progress bar yeets itself after awhile, so we'll just recreate it every 100000 ticks
            if (progressBarTicks > 10000)
            {
                ClearProgressBar();
                progressBarTicks = 0;
            }

            progressBar ??= EffecterDefOf.ProgressBarAlwaysVisible.Spawn();

            progressBar.EffectTick(new TargetInfo(Position + IntVec3.North.RotatedBy(Rotation), Map), TargetInfo.Invalid);
            var mote = ((SubEffecter_ProgressBar)progressBar.children[0]).mote;
            mote.progress = 1f - ((float)ticksRemaining / ExtractionTimeInTicks);
            mote.offsetZ = ProgressBarOffsetZ;
            mote.solidTimeOverride = ExtractionTimeInTicks;
            progressBarTicks++;
            //if (mote != null)
            //{

            //}
        }

        public enum GeneState { SinglePack, Multipack }

        public Dictionary<GeneDef, GeneState> GetAllGenesOnCurrentMap()
        {
            // Get the map this is placed in
            List<Thing> thingsOnMap = Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.GenepackHolder));

            // i = 1 in singlepack. in multipcak.
            Dictionary<GeneDef, GeneState> geneLookup = new Dictionary<GeneDef, GeneState>();

            foreach (Thing thing in thingsOnMap)
            {
                foreach (var genePack in thing.TryGetComp<CompGenepackContainer>().ContainedGenepacks)
                {
                    int genesInPack = genePack.GeneSet.GenesListForReading.Count;
                    foreach (var geneDef in genePack.GeneSet.GenesListForReading)
                    {
                        if (genesInPack > 1 && !geneLookup.ContainsKey(geneDef))
                        {
                            geneLookup[geneDef] = GeneState.Multipack;
                        }
                        else
                        {
                            geneLookup[geneDef] = GeneState.SinglePack;
                        }
                    }
                }
                if (thing.TryGetComp<Comp_GeneNode>() is Comp_GeneNode gnComp)
                {
                    foreach(var geneDef in gnComp.Props.geneList)
                    {
                        geneLookup[geneDef] = GeneState.SinglePack;
                    }
                    foreach (var geneSet in gnComp.Props.geneSetList)
                    {
                        foreach (var geneDef in geneSet.geneList)
                        {
                            if (!geneLookup.ContainsKey(geneDef))
                            {
                                geneLookup[geneDef] = GeneState.Multipack;
                            }
                        }
                    }
                }
            }
            return geneLookup;
        }

        private void Finish()
        {
            if (GetContainedPawn() != null)
            {
                Pawn containedPawn = GetContainedPawn();

                var existingGenes = GetAllGenesOnCurrentMap();
                var validPawnGenes = containedPawn.genes.GenesListForReading.Where(x => x.def.biostatArc == 0 || CanExtractArchite).Select(x => x.def).ToList();

                validPawnGenes.RemoveAll(x => AccessTools.Property(x.GetType(), "IsMutation") != null || AccessTools.Property(x.GetType(), "IsEvolution") != null);

                // Check if the gene-category is "BS_DO_NOT"
                validPawnGenes = validPawnGenes.Where(x => !x.displayCategory.defName.Contains("BS_DO_NOT")).ToList();

                var pickableGenes = validPawnGenes.OrderBy(x => Rand.Range(0, 1f)).ToList();

                // Check if baseliner
                if (IsBaselinerOrEquavalent(pickableGenes))
                {
                    AddBaselinerGenes(pickableGenes);
                }

                var newGenes = pickableGenes.Where(x => !existingGenes.ContainsKey(x)).ToList();
                var almostNewGenes = pickableGenes.Where(x => !existingGenes.ContainsKey(x) || (existingGenes.ContainsKey(x) && existingGenes[x] == GeneState.Multipack)).ToList();
                var pickableNewish = newGenes.Concat(almostNewGenes).ToHashSet().OrderBy(x => Rand.Range(0, 1f)).ToList();

                List<GeneDef> genesInPack = new();
                // Add initial Gene.
                if (targetGene == null)
                {
                    if (pickableNewish.Any())
                    {
                        genesInPack.Add(pickableNewish.Pop());
                    }
                    else
                    {
                        genesInPack.Add(pickableGenes.Pop());
                        Log.Message($"{containedPawn.Name} doesn't have any genes you don't have singles of. Adding a random gene from their geneset instead.");
                    }
                }
                else
                {
                    genesInPack.Add(targetGene);
                }

                if (Rand.Chance(Settings.megaMultipackChance))
                {
                    // Generate huge multipack
                    int numberOfGenes = Rand.Range(3, 16);
                    while (numberOfGenes > 0 && pickableGenes.Any())
                    {
                        genesInPack.Add(pickableGenes.Pop());
                        numberOfGenes--;
                    }
                }
                else if (Rand.Chance(Settings.multipackChance))
                {
                    // Generate multipack
                    int numberOfGenes = Rand.Range(1, 3);
                    while (numberOfGenes > 0 && pickableGenes.Any())
                    {
                        genesInPack.Add(pickableGenes.Pop());
                        numberOfGenes--;
                    }
                }
                else
                {
                    targetGene = null;
                }
                var genesInPackListOfLists = new List<List<GeneDef>>();
                if (Rand.Chance(Settings.splitZeroCost))
                {
                    // Create two packs, one with zero cost genes and one with the rest.
                    var zeroCostGenes = genesInPack.Where(x => x.biostatArc == 0 && x.biostatMet == 0 && x.biostatCpx <=1).ToList();
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
                }
            }
            ticksRemaining = ExtractionTimeInTicks;
            startTick = Find.TickManager.TicksGame;
        }

        private static bool IsBaselinerOrEquavalent(List<GeneDef> pickableGenes)
        {
            return pickableGenes.All(x => x.defName.ToLower().Contains("skin") || x.defName.ToLower().Contains("hair")) || pickableGenes.Count == 0;
        }

        private static void AddBaselinerGenes(List<GeneDef> pickableGenes)
        {
            // Add the "Baseliner" set of genes. E.g. Human Headbone etc.
            List<string> baselinerGenes = new() { "GET_SleepRegular", "GET_ViolenceNormal", "GET_Learning_Normal", "GET_HumanLegs", "GET_AverageApperance", "GET_BodySizeNormal", "AG_NoWings", "AG_NoAntennae", "AG_NoTusks", "AG_NoLowerAntennae",
                        "Jaw_Baseline", "Hands_Human", "Ears_Human", "Nose_Human", "Headbone_Human", "Voice_Human", "Body_Hulk", "Body_Standard", "Body_Thin", "Body_Fat", "GET_RegularAddiction", "GET_RegularBodyShape" };
            // Get all defs
            var geneDefs = DefDatabase<GeneDef>.AllDefs.Where(x => baselinerGenes.Any(bg => x.defName.Contains(bg))).ToList();
            pickableGenes.AddRange(geneDefs);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref containedNutrition, "containedNutrition", 0f);
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings");
            Scribe_Values.Look(ref overchargeActive, "overchargeActive", false);
            Scribe_Defs.Look(ref targetGene, "targetGene");
            if (allowedNutritionSettings == null)
            {
                allowedNutritionSettings = new StorageSettings(this);
                if (def.building.defaultStorageSettings != null)
                {
                    allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
                }
            }
        }

        public bool CanAcceptNutrition(Thing thing)
        {
            return allowedNutritionSettings.AllowedToAccept(thing);
        }

        public StorageSettings GetStoreSettings()
        {
            return allowedNutritionSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }

    }

}