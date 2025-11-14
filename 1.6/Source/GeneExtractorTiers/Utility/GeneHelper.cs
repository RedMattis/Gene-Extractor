using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GeneExtractorTiers.Utility;

public static class GeneHelper
{
    public static bool IsBaselinerOrEquavalent(List<GeneDef> pickableGenes)
    {
        return pickableGenes
            .All(
                x =>
                    x.defName.ToLower().Contains("skin")
                    || x.defName.ToLower().Contains("hair")
                    //TODO: (LFS) Genes Expanded: Eyes support
                )
            || pickableGenes.Count == 0;
    }

    public static void AddBaselinerGenes(List<GeneDef> pickableGenes)
    {
        // Add the "Baseliner" set of genes. E.g. Human Headbone etc.
        List<string> baselinerGenes = 
        [
            "GET_SleepRegular",
            "GET_ViolenceNormal",
            "GET_Learning_Normal",
            "GET_HumanLegs",
            "GET_AverageApperance",
            "GET_BodySizeNormal",
            "AG_NoWings",
            "AG_NoAntennae",
            "AG_NoTusks",
            "AG_NoLowerAntennae",
            "Jaw_Baseline",
            "Hands_Human",
            "Ears_Human",
            "Nose_Human",
            "Headbone_Human",
            "Voice_Human",
            "Body_Hulk",
            "Body_Standard",
            "Body_Thin",
            "Body_Fat",
            "GET_RegularAddiction",
            "GET_RegularBodyShape",
        ];

        // Get all defs
        var geneDefs = DefDatabase<GeneDef>.AllDefs.Where(x => baselinerGenes.Any(bg => x.defName.Contains(bg))).ToList();
        pickableGenes.AddRange(geneDefs);
    }
}
