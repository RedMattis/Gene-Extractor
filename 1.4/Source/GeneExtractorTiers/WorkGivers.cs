using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GeneExtractorTiers
{
	public class WorkGiver_HaulToGrowthGeneExtractor_II : WorkGiver_Scanner
	{
		private const float NutritionBuffer = 2.5f;
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_II);
		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
            if (t is not Building_GeneExtractorTier geneVat)
            {
                return null;
            }

            //Building_GeneExtractorTier geneVat = (Building_GeneExtractorTier)t;
			if (geneVat.NutritionNeeded > 0f)
			{
				ThingCount thingCount = FindNutrition(pawn, geneVat);
				if (thingCount.Thing != null)
				{
					Job job = HaulAIUtility.HaulToContainerJob(pawn, thingCount.Thing, t);
					job.count = Mathf.Min(job.count, thingCount.Count);
					return job;
				}
			}
			return null;
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
			{
				return false;
			}
			if (t.IsBurning())
			{
				return false;
			}
            if (t is not Building_GeneExtractorTier geneVat)
            {
                return false;
            }
            //Building_GeneExtractorTier geneVat = (Building_GeneExtractorTier)t;
			if (geneVat.NutritionNeeded > 2.5f)
			{
				if (FindNutrition(pawn, geneVat).Thing == null)
				{
					JobFailReason.Is("NoFood".Translate());
					return false;
				}
				return true;
			}
			return false;
		}

		private ThingCount FindNutrition(Pawn pawn, Building_GeneExtractorTier vat)
		{
			Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Validator);
			if (thing == null)
			{
				return default(ThingCount);
			}
			int b = Mathf.CeilToInt(vat.NutritionNeeded / thing.GetStatValue(StatDefOf.Nutrition));
			return new ThingCount(thing, Mathf.Min(thing.stackCount, b));
			bool Validator(Thing x)
			{
				if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
				{
					return false;
				}
				if (!vat.CanAcceptNutrition(x))
				{
					return false;
				}
				if (x.def.GetStatValueAbstract(StatDefOf.Nutrition) > vat.NutritionNeeded)
				{
					return false;
				}
				return true;
			}
		}
	}

	//public class WorkGiver_HaulToGrowthGeneExtractor_II : WorkGiver_HaulToGrowthGeneExtractor_II
	//{
	//	public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_III);
	//}


	public class WorkGiver_CarryToGeneExtractor_II : WorkGiver_CarryToBuilding
	{
		public override ThingRequest ThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_II);

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (!base.ShouldSkip(pawn, forced))
			{
				return !ModsConfig.BiotechActive;
			}
			return true;
		}
	}

    public class WorkGiver_HaulToGrowthGeneExtractor_III : WorkGiver_HaulToGrowthGeneExtractor_II
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_III);
    }

    public class WorkGiver_CarryToGeneExtractor_III : WorkGiver_CarryToBuilding
	{
		public override ThingRequest ThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_III);

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (!base.ShouldSkip(pawn, forced))
			{
				return !ModsConfig.BiotechActive;
			}
			return true;
		}
	}

    public class WorkGiver_HaulToGrowthGeneExtractor_IV : WorkGiver_HaulToGrowthGeneExtractor_II
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_IV);
    }

    public class WorkGiver_CarryToGeneExtractor_IV : WorkGiver_CarryToBuilding
    {
        public override ThingRequest ThingRequest => ThingRequest.ForDef(DefOfs.GET_GeneExtractor_IV);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!base.ShouldSkip(pawn, forced))
            {
                return !ModsConfig.BiotechActive;
            }
            return true;
        }
    }

}
