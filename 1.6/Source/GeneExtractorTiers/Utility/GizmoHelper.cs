using System;
using GeneExtractorTiers.Extractors;
using RimWorld;
using Verse;

namespace GeneExtractorTiers.Utility
{
    public static class GizmoHelper
    {
        public static Command_Action BuildGizmoSelectGene(Action action)
        {
            return new()
            {
                defaultLabel = "GET_SelectGene".Translate(),
                defaultDesc = "GET_SelectGeneDesc".Translate(),
                icon = Textures.TargetGeneIcon, // FIX ICON.
                action = action,
            };
        }

        public static Command_Action BuildGizmoCancelExtraction(Action cancel)
        {
            return new()
            {
                defaultLabel = "CommandCancelExtraction".Translate(),
                defaultDesc = "CommandCancelExtractionDesc".Translate(),
                icon = Textures.CancelIcon,
                activateSound = SoundDefOf.Designate_Cancel,
                action = cancel,
            };
        }

        public static Command_Action BuildGizmoCancelLoad(Action cancelLoad)
        {
            return new()
            {
                defaultLabel = "CommandCancelLoad".Translate(),
                defaultDesc = "CommandCancelLoadDesc".Translate(),
                icon = Textures.CancelIcon,
                activateSound = SoundDefOf.Designate_Cancel,
                action = cancelLoad,
            };
        }

        public static Command_Action BuildGizmoInsertPawn(Action pawnSelector, bool powered)
        {
            var insertPerson = new Command_Action()
            {
                defaultLabel = "InsertPerson".Translate() + "...",
                defaultDesc = "InsertPersonGeneExtractorDesc".Translate(),
                icon = Textures.InsertPawn,
                action = pawnSelector,
            };

            if (!powered)
            {
                insertPerson.Disable("NoPower".Translate().CapitalizeFirst());
            }

            return insertPerson;
        }

        public static Command_Action BuildGizmoOverdriveDeactivate(Action deactivateOverdrive)
        {
            return new Command_Action
            {
                defaultLabel = "GET_DeactivateOverdrive".Translate(),
                defaultDesc = "GET_DeactivateOverdriveDesc".Translate(),
                icon = Textures.CancelOverdrive,
                action = deactivateOverdrive,
            };
        }

        public static Command_Action BuildGizmoOverdriveActivate(Action activateOverdrive)
        {
            return new Command_Action
            {
                defaultLabel = "GET_ActivateOverdrive".Translate(),
                defaultDesc = "GET_ActivateOverdriveDesc".Translate(),
                icon = Textures.ActivateOverdrive,
                action = activateOverdrive,
            };
        }

        public static Command_Action BuildGizmoOverdrive(bool overdriveActive, Action activateOverdrive, Action deactivateOverdrive)
        {
            if (overdriveActive)
            {
                return GizmoHelper.BuildGizmoOverdriveDeactivate(deactivateOverdrive);
            }
            else
            {
                return GizmoHelper.BuildGizmoOverdriveActivate(activateOverdrive);
            }
        }
    }
}
