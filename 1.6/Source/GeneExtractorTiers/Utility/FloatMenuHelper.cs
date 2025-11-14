using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GeneExtractorTiers.Utility;

public static class FloatMenuHelper
{
    public static void OpenFloatMenuGenePicker(Pawn selectedPawn, Map map, Action<GeneDef> setTargetGene)
    {
        var list = new List<FloatMenuOption>();
        var allPawnGenes = selectedPawn.genes.GenesListForReading.Select(x => x.def).ToList();
        if (GeneHelper.IsBaselinerOrEquavalent(allPawnGenes))
        {
            GeneHelper.AddBaselinerGenes(allPawnGenes);
        }

        foreach (var gene in allPawnGenes)
        {
            var existingGenes = GeneHelper.GetAllGenesOnMap(map);
            if (existingGenes.ContainsKey(gene) && existingGenes[gene] == GeneState.SinglePack)
            {
                continue;
            }

            list.Add(new FloatMenuOption(gene.LabelCap, delegate
            {
                setTargetGene(gene);
            }));
        }

        Find.WindowStack.Add(new FloatMenu(list));
    }

    public static void BuildFloatMenuAvailablePawns(Map map, Func<Pawn, AcceptanceReport> canAcceptPawn, Action<Pawn> selectPawn)
    {
        List<FloatMenuOption> list = [];
        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (pawn.genes != null)
            {
                AcceptanceReport acceptanceReport = canAcceptPawn(pawn);
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
                    list.Add(new FloatMenuOption(text, () => selectPawn(pawn), pawn, Color.white));
                }
            }
        }

        if (!list.Any())
        {
            list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
        }

        Find.WindowStack.Add(new FloatMenu(list));
    }
}
