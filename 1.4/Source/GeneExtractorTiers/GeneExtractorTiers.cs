using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace GeneExtractorTiers
{
    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_II : Building_GeneExtractorTier
    {
    }

    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_III : Building_GeneExtractorTier
    {
        public override bool CanExtractArchite => true;
    }

    [StaticConstructorOnStartup]
    public class Build_GeneExtractorTier_IV : Building_GeneExtractorTier
    {
        public override bool CanExtractArchite => true;
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
