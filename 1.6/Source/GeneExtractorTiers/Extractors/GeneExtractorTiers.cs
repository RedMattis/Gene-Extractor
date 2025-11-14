using RimWorld;
using Verse;

namespace GeneExtractorTiers.Extractors
{
    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_II : GeneExtractorNutritionBase
    {
    }

    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_III : GeneExtractorNutritionBase
    {
        public override bool CanExtractArchite => true;
        public override float SpeedMultiplier => 1.25f;
    }

    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_IV : GeneExtractorNutritionBase
    {
        public override bool CanExtractArchite => true;
        public override bool CanTargetExtraction => true;
        public override float SpeedMultiplier => 2.5f;
    }


    [DefOf]
    public static class DefOfs
    {
        public static ThingDef GET_GeneExtractor_II;
        public static ThingDef GET_GeneExtractor_III;
        public static ThingDef GET_GeneExtractor_IV;
    }

}
