using HarmonyLib;
using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace GeneExtraction_VNPE_Patch
{
    //public static class VatPatch
    //   {
    //	public static float PatchTick(float ___containedNutrition, Building_GrowthVat __instance)
    //	{
    //		if (__instance.IsHashIntervalTick(250))
    //		{
    //			var compResource = __instance.GetComp<CompResource>();
    //			if (compResource != null && compResource.PipeNet is PipeNet net)
    //			{
    //				var stored = net.Stored;
    //				var needed = __instance.NutritionNeeded;
    //				while (needed > 0.9f && stored > 0)
    //				{
    //					___containedNutrition += 0.9f;
    //					net.DrawAmongStorage(1, net.storages);

    //					stored--;
    //					needed -= 0.9f;
    //				}
    //			}
    //		}

    //		return ___containedNutrition;
    //	}
    //}

    //[HarmonyPatch(typeof(Build_GeneExtractorTier_II))]
    //[HarmonyPatch("Tick", MethodType.Normal)]
    //public static class Building_GeneVatII_Tick
    //{
    //	public static void Postfix(ref float ___containedNutrition, Building_GrowthVat __instance)
    //       {
    //           ___containedNutrition = VatPatch.PatchTick(___containedNutrition, __instance);
    //       }
    //   }

    //[HarmonyPatch(typeof(Build_GeneExtractorTier_III))]
    //[HarmonyPatch("Tick", MethodType.Normal)]
    //public static class Building_GeneVatIII_Tick
    //{
    //	public static void Postfix(ref float ___containedNutrition, Building_GrowthVat __instance)
    //	{
    //		___containedNutrition = VatPatch.PatchTick(___containedNutrition, __instance);
    //	}
    //}


    public static class Building_GeneExtractorTier
    {
        public static void Postfix(Building __instance)
        {

            if (!__instance.IsHashIntervalTick(250))
            {
                return;
            }
            CompResource comp = __instance.GetComp<CompResource>();
            //Log.Warning($"\nCompRefuelable is {comp}\n{__instance.Label}");
            if (comp == null)
            {
                return;
            }
            PipeNet pipeNet = comp.PipeNet;
            if (pipeNet != null)
            {
                float storedMeals = pipeNet.Stored;
                var type = __instance.GetType();
                var nutritionNeededProp = AccessTools.Property(type, "NutritionNeeded");
                var containedNutField = AccessTools.Field(type, "containedNutrition");

                float nutritionNeed = (float)nutritionNeededProp.GetValue(__instance);
                float containedNutrition = (float)containedNutField.GetValue(__instance);
                while (nutritionNeed > 0.1f && storedMeals > 0f)
                {
                    containedNutrition = containedNutrition + 0.9f;
                    containedNutField.SetValue(__instance, containedNutrition);
                    pipeNet.DrawAmongStorage(1f, pipeNet.storages);
                    storedMeals -= 1f;
                    nutritionNeed -= 0.9f;
                }
            }
            //else
            //{
            //    Log.Warning("DEBUG!! PIPENET IS NULL!.");
            //}

            
        }
    }

}
