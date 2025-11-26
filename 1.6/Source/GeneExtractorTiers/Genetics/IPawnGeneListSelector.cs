using System.Collections.Generic;
using Verse;

namespace GeneExtractorTiers.Genetics;

/// <summary>Defines operations for selecting lists of genes from a <see cref="Pawn" /></summary>
public interface IPawnGeneListSelector
{
    /// <summary>Generates the collection of <see cref="GeneDef" /> that can be extracted from <paramref name="pawn" /></summary>
    /// <param name="pawn"><see cref="Pawn" /> whose genetics are being evaluated</param>
    /// <param name="canExtractArchite">Flag indicating whether archite genes should be included in the gene list</param>
    /// <returns>The collection of <see cref="GeneDef" /> that can be extracted from <paramref name="pawn" /></returns>
    public List<GeneDef> GetPawnGeneListForExtraction(Pawn pawn, bool canExtractArchite);

    /// <summary>Builds the collection of <see cref="GeneDef" /> for <paramref name="pawn" /> that will go into a new genepack.</summary>
    /// <param name="pawn"><see cref="Pawn" /> whose genetics are being evaluated</param>
    /// <param name="targetGene">Specific <see cref="GeneDef" /> targeted for extraction</param>
    /// <param name="pickableGenes">Collection of <see cref="GeneDef" /> that can be chosen from</param>
    /// <param name="pickableNewish">Collection of <see cref="GeneDef" /> that are already extracted and exist on the current map in a multipack</param>
    /// <param name="chanceMegaPack">Chance of a megapack (4-16) genes being generated</param>
    /// <param name="chanceMultiPack">Chance of a multipack (2-4) genes being generated</param>
    /// <returns>The collection of <see cref="GeneDef" /> selected for a new genepack</returns>
    List<GeneDef> BuildGenePackGeneListFromPawn(Pawn pawn,
        GeneDef targetGene, List<GeneDef> pickableGenes, List<GeneDef> pickableNewish,
        float chanceMegaPack, float chanceMultiPack);
}
