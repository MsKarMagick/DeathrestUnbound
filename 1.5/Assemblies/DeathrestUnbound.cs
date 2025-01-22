using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeathrestUnbound
{
    [StaticConstructorOnStartup]
    public static class HarmonyInitialization
    {
        static HarmonyInitialization()
        {
            var harmony = new Harmony("MsKarMagick.deathrestunbound");
            // Harmony.DEBUG = true;

            try
            {
                harmony.PatchAll();
                Log.Message("[DeathrestUnbound] Harmony patches applied successfully.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DeathrestUnbound] Error during Harmony initialization: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_DeathrestCasket))]
    public static class DeathrestCasketPatches
    {
        [HarmonyPatch("TryUnassignPawn")]
        [HarmonyPostfix]
        public static void PostfixTryUnassignPawn(Pawn pawn, CompAssignableToPawn_DeathrestCasket __instance)
        {
           // Log.Message($"[DeathrestUnbound] PostfixTryUnassignPawn called for {pawn.Name}");

            var comp = __instance.parent.TryGetComp<CompDeathrestBindable>();

            if (comp != null && comp.BoundPawn == pawn)
            {
               // Log.Message($"[DeathrestUnbound] Clearing binding for pawn {pawn.Name}");

                // Properly unbind through the method
                comp.Notify_DeathrestGeneRemoved();

                // Update the Gene_Deathrest
                var geneDeathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                if (geneDeathrest != null)
                {
                   // Log.Message($"[DeathrestUnbound] Removing casket from Gene_Deathrest for {pawn.Name}");
                    geneDeathrest.BoundBuildings.Remove(comp.parent);
                }

                // Release the reservation if it exists
                if (pawn.MapHeld?.reservationManager != null)
                {
                   // Log.Message($"[DeathrestUnbound] Releasing reservation on casket for {pawn.Name}");
                    pawn.MapHeld.reservationManager.ReleaseAllForTarget(__instance.parent);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Verse.AI.Pawn_JobTracker))]
    public static class JobTrackerPatches
    {
        [HarmonyPatch("StartJob")]
        [HarmonyPrefix]
        public static bool PrefixStartJob(Verse.AI.Pawn_JobTracker __instance, Verse.AI.Job newJob)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            if (newJob.def == JobDefOf.Deathrest)
            {
                var casket = newJob.GetTarget(TargetIndex.A).Thing;

                if (casket == null)
                {
                   // Log.Warning($"[DeathrestUnbound] Pawn {pawn.Name} attempted to start Deathrest with no valid casket. Cancelling job.");
                    return false;
                }

                var assignableComp = casket.TryGetComp<CompAssignableToPawn_DeathrestCasket>();

                // Automatically assign the pawn to the casket if not already assigned
                if (assignableComp != null && !assignableComp.AssignedPawns.Contains(pawn))
                {
                   // Log.Message($"[DeathrestUnbound] Automatically assigning {pawn.Name} to casket {casket}.");
                    assignableComp.TryAssignPawn(pawn);
                }

                // Allow the first deathrest attempt even if the binding is not yet established
                var geneDeathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                if (geneDeathrest != null && !geneDeathrest.BoundBuildings.Contains(casket))
                {
                   // Log.Message($"[DeathrestUnbound] Gene_Deathrest is not yet bound to the casket for {pawn.Name}. Allowing the first deathrest attempt.");
                }

               // Log.Message($"[DeathrestUnbound] Starting Deathrest job for {pawn.Name} in casket {casket}.");
            }

            return true; // Proceed with valid jobs
        }
    }

    [HarmonyPatch(typeof(JobDriver_Deathrest))]
    public static class JobDriverDeathrestPatches
    {
        [HarmonyPatch("GetReport")]
        [HarmonyPrefix]
        public static bool PrefixGetReport(JobDriver_Deathrest __instance)
        {
            var pawn = __instance.pawn;

            // Suppress warnings during job queueing as casket binding may not be finalized
            if (pawn.CurJob?.def == JobDefOf.Deathrest)
            {
                //Log.Message($"[DeathrestUnbound] Suppressed warning for GetReport: Pawn {pawn.Name} is en route to Deathrest.");
                return false;
            }

            var casket = RestUtility.CurrentBed(pawn);

            if (casket == null)
            {
               // Log.Warning($"[DeathrestUnbound] GetReport error: Pawn {pawn.Name} has no valid casket.");
                return false; // Prevent further errors
            }

            return true; // Proceed with valid report generation
        }
    }
}
