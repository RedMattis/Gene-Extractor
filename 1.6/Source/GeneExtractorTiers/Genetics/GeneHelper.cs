using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace GeneExtractorTiers.Genetics;

public class GeneHelper
{
    public bool IsBaselinerOrEquavalent(IEnumerable<GeneDef> pickableGenes)
    {
        return !pickableGenes.Any()
            || pickableGenes
                .All(
                    x =>
                        x.defName.ToLower().Contains("skin")
                        || x.defName.ToLower().Contains("hair")
                        //TODO: (LFS) Genes Expanded: Eyes support
                    );
    }

    public void AddBaselinerGenes(List<GeneDef> pickableGenes)
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

    public Dictionary<GeneDef, GeneState> GetAllGenesOnMap(Map currentMap)
    {
        // Get the map this is placed in
        List<Thing> thingsOnMap = currentMap.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.GenepackHolder));

        // i = 1 in singlepack. in multipcak.
        Dictionary<GeneDef, GeneState> geneLookup = [];

        foreach (Thing thing in thingsOnMap)
        {
            var genepackList = thing.TryGetComp<CompGenepackContainer>()?.ContainedGenepacks;
            if (genepackList != null)
            {
                foreach (var genePack in genepackList)
                {
                    int genesInPack = genePack.GeneSet.GenesListForReading.Count;
                    foreach (var geneDef in genePack.GeneSet.GenesListForReading)
                    {
                        if (genesInPack > 1 && !geneLookup.ContainsKey(geneDef))
                        {
                            geneLookup[geneDef] = GeneState.Multipack;
                        }
                        else if (genesInPack == 1)
                        {
                            geneLookup[geneDef] = GeneState.SinglePack;
                        }
                    }
                }
            }
            if (thing.TryGetComp<Comp_GeneNode>() is Comp_GeneNode gnComp)
            {
                foreach (var geneDef in gnComp.Props.geneList)
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

    public List<GeneDef> BuildGeneListFromPawn(Pawn containedPawn,
        ref GeneDef targetGene, List<GeneDef> pickableGenes, List<GeneDef> pickableNewish,
        float chanceMegaPack, float chanceMultiPack)
    {
        List<GeneDef> genesInPack = [];

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

        if (Rand.Chance(chanceMegaPack))
        {
            // Generate huge multipack
            int numberOfGenes = Rand.Range(3, 16);
            while (numberOfGenes > 0 && pickableGenes.Any())
            {
                genesInPack.Add(pickableGenes.Pop());
                numberOfGenes--;
            }
        }
        else if (Rand.Chance(chanceMultiPack))
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

        return genesInPack;
    }
}
