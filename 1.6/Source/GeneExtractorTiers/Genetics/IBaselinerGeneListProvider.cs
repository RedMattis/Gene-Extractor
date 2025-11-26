using System.Collections.Generic;
using Verse;

namespace GeneExtractorTiers.Genetics;

/// <summary>Defines operations for providing lists of genes for Baseliner xenotypes with no other exceptional genetics.</summary>
public interface IBaselinerGeneListProvider
{
    /// <summary>Evaluates whether <paramref name="pickableGenes" /> represents a baseliner</summary>
    /// <param name="pickableGenes">Collection of <see cref="GeneDef"/> to evaluate</param>
    /// <returns>Flag indicating whether <paramref name="pickableGenes" /> represents a baseliner</returns>
    bool IsBaselinerOrEquavalent(IEnumerable<GeneDef> pickableGenes);

    /// <summary>Adds custom baseliner <see cref="GeneDef" /> from this mod to <paramref name="pickableGenes" /> and returns the result</summary>
    /// <param name="pickableGenes">Collection of <see cref="GeneDef"/> to append to</param>
    /// <returns>The resulting collection of <see cref="GeneDef" /></returns>
    IEnumerable<GeneDef> AddBaselinerGenes(IEnumerable<GeneDef> pickableGenes);
}
