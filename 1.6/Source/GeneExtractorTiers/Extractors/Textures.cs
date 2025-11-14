using UnityEngine;
using Verse;

namespace GeneExtractorTiers.Extractors
{
    public static class Textures
    {
        public static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public static readonly Texture2D ActivateOverdrive = ContentFinder<Texture2D>.Get("GET_OverdriveOn");
        public static readonly Texture2D CancelOverdrive = ContentFinder<Texture2D>.Get("GET_OverdriveOff");
        public static readonly Texture2D TargetGeneIcon = ContentFinder<Texture2D>.Get("GET_TargetGene");
        public static readonly Texture2D InsertPawn = ContentFinder<Texture2D>.Get("UI/Gizmos/InsertPawn");
    }
}
