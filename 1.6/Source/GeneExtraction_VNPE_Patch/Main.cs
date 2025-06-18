using HarmonyLib;
using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace GeneExtraction_VNPE_Patch
{
	[StaticConstructorOnStartup]
	public class VNPEPatchMain
	{
		static VNPEPatchMain()
		{
			var harmony = new Harmony("GET-VNPE_COMPAT");
            //harmony.PatchAll();
            Type typeT2 = AccessTools.TypeByName("GeneExtractorTiers.Building_GeneExtractorTier");
            if (typeT2 != null)
            {
                MethodInfo method = typeT2.GetMethod("Tick");
                MethodInfo method2 = typeof(Building_GeneExtractorTier).GetMethod("Postfix");
                harmony.Patch(method, null, new HarmonyMethod(method2), null, null);
            }

            //Type typeT3 = AccessTools.TypeByName("GeneExtractorTiers.Build_GeneExtractorTier_III");
            //if (typeT3 != null)
            //{
            //    MethodInfo method = typeT3.GetMethod("Tick");
            //    MethodInfo method2 = typeof(Building_GeneExtractorTier).GetMethod("Postfix");
            //    harmony.Patch(method, null, new HarmonyMethod(method2), null, null);
            //}

            Log.Message($"[Gene Extraction Tiers] Compatibility for Vanilla Nutrient Paste Expanded loaded.");
		}
	}

	


}
