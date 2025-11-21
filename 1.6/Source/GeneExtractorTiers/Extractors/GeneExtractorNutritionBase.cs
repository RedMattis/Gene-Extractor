using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GeneExtractorTiers.Extractors
{
    [StaticConstructorOnStartup]
    public class GeneExtractorNutritionBase : GeneExtractorBase, IStoreSettingsParent
    {
        // Starvation
        public float BiostarvationDailyOffset
        {
            get
            {
                if (!Working)
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

        private float BiostarvationSeverityPercent
        {
            get
            {
                if (TargetSelected)
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


        // Nutrition
        private const float OverchargeNutritionFactor = 3.0f;

        private float containedNutrition;
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
                if (OverchargeActive)
                {
                    num *= OverchargeNutritionFactor;
                }
                return num;
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


        // Storage Settings
        private StorageSettings allowedNutritionSettings;

        public bool StorageTabVisible => true;

        public bool CanAcceptNutrition(Thing thing)
        {
            return allowedNutritionSettings.AllowedToAccept(thing);
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


        // Float Menu
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


        // Gizmos
        protected override IEnumerable<Gizmo> BuildGizmosDevGizmos()
        {
            foreach (var gizmo in base.BuildGizmosDevGizmos())
            {
                yield return gizmo;
            }

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

        protected override IEnumerable<Gizmo> BuildGizmosSettings()
        {
            foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
            {
                yield return item;
            }
        }


        //Inspect String
        protected override void InspectStringAddResourceStarvation(StringBuilder stringBuilder)
        {
            float biostarvationSeverityPercent = BiostarvationSeverityPercent;
            if (biostarvationSeverityPercent > 0f)
            {
                string text = ((BiostarvationDailyOffset >= 0f) ? "+" : string.Empty);
                stringBuilder
                    .AppendLineIfNotEmpty()
                    .Append(string.Format("{0}: {1} ({2})", "Biostarvation".Translate(), biostarvationSeverityPercent.ToStringPercent(), "PerDay".Translate(text + BiostarvationDailyOffset.ToStringPercent())));
            }
        }

        protected override void InspectStringAddResourceConsumption(StringBuilder stringBuilder)
        {

            stringBuilder.AppendLineIfNotEmpty().Append("Nutrition".Translate()).Append(": ")
                .Append(NutritionStored.ToStringByStyle(ToStringStyle.FloatMaxOne));

            if (Working)
            {
                stringBuilder.Append(" (-").Append("PerDay".Translate((NutritionConsumedPerDay * Settings.nutritionMultiplier).ToString("F1"))).Append(")");
            }
        }

        public override AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            if (!pawn.IsColonist && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony)// && (!pawn.I || !pawn.IsGhoul))
            {
                return false;
            }
            if (TargetSelected && selectedPawn != pawn)
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
            if (innerContainer.Any(x => x is Pawn))
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


        // Tick
        protected override bool Tick_ResourceStarvation()
        {
            if (TargetSelected)
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
                return true;
            }

            return false;
        }

        protected override void Tick_ConsumeResources()
        {
            containedNutrition = Mathf.Clamp(containedNutrition - NutritionConsumedPerDay * Settings.nutritionMultiplier / 60000f, 0f, 2.1474836E+09f);
            if (containedNutrition <= 0f)
            {
                TryAbsorbNutritiousThing();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref containedNutrition, "containedNutrition", 0f);
            Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings");
            if (allowedNutritionSettings == null)
            {
                allowedNutritionSettings = new StorageSettings(this);
                if (def.building.defaultStorageSettings != null)
                {
                    allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
                }
            }
        }
    }
}
