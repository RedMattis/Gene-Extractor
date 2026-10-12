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
    [HarmonyPatch]
    public static class Building_GeneExtractorTier
    {
        [HarmonyPatch(typeof(GeneExtractorTiers.Building_GeneExtractorTier), "Tick")]
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
        }
    }

}
