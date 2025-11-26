using System.Collections.Generic;
using Verse;

namespace GeneExtractorTiers.Genetics;

/// <summary>Defines operations for providing lists of genes from the current map.</summary>
public interface IMapGeneListProvider
{
    /// <summary>Generates a <see cref="Dictionary{GeneDef, GeneState}" /> for all currently owned genes on the current map.</summary>
    /// <param name="currentMap">Current <see cref="Map" /></param>
    /// <returns>A <see cref="Dictionary{GeneDef, GeneState}" /> for all currently owned genes on the current map.</returns>
    Dictionary<GeneDef, GeneState> GetAllGenesOnMap(Map currentMap);
}
