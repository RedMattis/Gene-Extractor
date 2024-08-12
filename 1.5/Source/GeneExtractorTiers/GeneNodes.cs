using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static System.Net.Mime.MediaTypeNames;

namespace GeneExtractorTiers
{
    // Props
    public class CompProperties_GeneNode : CompProperties
    {
        public List<GeneDef> geneList = new();
        public List<GeneSetList> geneSetList = new();
        public string Faction = "LoS_ViperFamily";
        public int FactionGoodwill = 10;

        public CompProperties_GeneNode()
        {
            compClass = typeof(Comp_GeneNode);
        }
    }

    public class GeneSetList
    {
        public List<GeneDef> geneList = new();
    }

    internal class Comp_GeneNode : ThingComp, IThingHolder
    {
        public CompProperties_GeneNode Props => (CompProperties_GeneNode)props;

        public ThingOwner<Thing> innerContainer = null;
        public bool firstTimePlaced = true;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            innerContainer.ClearAndDestroyContents();
            base.PostDestroy(mode, previousMap);
        }

        public override void PostDeSpawn(Map map)
        {
            innerContainer.ClearAndDestroyContents();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();

        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SetupContents();
            if (firstTimePlaced && !respawningAfterLoad) FirstTimePlacedSetup();
        }

        private void FirstTimePlacedSetup()
        {
            // Iterrate all factions, if the defname matches, add relation with the player faction.
            foreach (Faction faction in Find.FactionManager.AllFactions)
            {
                if (faction.def.defName == Props.Faction)
                {
                    faction.TryAffectGoodwillWith(Faction.OfPlayer, Props.FactionGoodwill);
                }
            }

            try
            {
                if (parent?.MapHeld?.IsPlayerHome == true && Props.Faction == "GET_DarkArchotech" && ModsConfig.AnomalyActive)
                {
                    // Spawn a shark of dark archotech.
                    //<MessageShardDropped>{0} dropped a shard of dark archotechnology. You can collect and make use of it.</MessageShardDropped>
                    GenDrop.TryDropSpawn(ThingMaker.MakeThing(ThingDefOf.Shard), parent.PositionHeld, parent.MapHeld, ThingPlaceMode.Near, out var resultingThing);
                    Messages.Message("MessageShardDropped".Translate(parent.Label).CapitalizeFirst(), resultingThing, MessageTypeDefOf.PositiveEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error("GeneExtractorTiers: Comp_GeneNode.PostPostMake encountered an error when trying to drop a Shard: " + ex);
            }
        }

        private void SetupContents()
        {
            innerContainer = new ThingOwner<Thing>(this);

            foreach (GeneDef gene in Props.geneList)
            {
                Genepack genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
                genepack.Initialize(new List<GeneDef> { gene });
                innerContainer.TryAdd(genepack);
            }
            foreach (GeneSetList geneSet in Props.geneSetList)
            {
                Genepack genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
                genepack.Initialize(geneSet.geneList);
                innerContainer.TryAdd(genepack);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref firstTimePlaced, "firstTimePlaced", true);
        }
    }

    [HarmonyPatch(typeof(Building_GeneAssembler), "CheckAllContainersValid")]
    public class Building_GeneExtractor_CheckAllContainersValid
    {
        public static bool Prefix()
        {
            // Literally don't see the point of this method. Are Ludeon concerned that players will sell the genepacks while assembling?
            // Eh Whatever. I don't see anyone else patching this method either, so I'll just make it be skipped.
            return false;
        }
    }

    // Harmony Patch for the Gene Assembler's GetGenePacks method.
    [HarmonyPatch(typeof(Building_GeneAssembler), nameof(Building_GeneAssembler.GetGenepacks))]
    public class Building_GeneAssembler_GetGenepacks
    {
        public static void Postfix(Building_GeneAssembler __instance, ref List<Genepack> __result, bool includePowered, bool includeUnpowered)
        {
            if (includePowered == false) return;
            try
            {
                List<Thing> connectedFacilities = __instance?.ConnectedFacilities;
                if (__instance == null)
                {
                    Log.Warning("Building_GeneAssembler instance is null!");
                }
                if (connectedFacilities != null)
                {
                    foreach (Thing item in __instance.ConnectedFacilities)
                    {
                        if (item.TryGetComp<Comp_GeneNode>() is Comp_GeneNode comp && item.TryGetComp<CompPowerTrader>()?.PowerOn == true)
                        {
                            // For people from before the rework we're going to need to initialize the inner container.
                            if (comp.innerContainer == null)
                            {
                                comp.PostPostMake();
                            }

                            foreach (Genepack gPack in comp.innerContainer.Cast<Genepack>())
                            {
                                __result.Add(gPack);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("GeneExtractorTiers: Building_GeneAssembler.GetGenepacks postfix encountered an error: " + ex);
            }
        }
    }
}
