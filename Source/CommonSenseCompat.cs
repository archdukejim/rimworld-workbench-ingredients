using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkbenchIngredients
{
    /// <summary>
    /// Soft compatibility with Common Sense (avilmask.CommonSense) for the "Drop at bench" output mode.
    ///
    /// Common Sense's "haul products over bills" is an OPPORTUNISTIC task — <c>OpportunisticTasks.
    /// Hauling_Opportunity(Job billJob, Pawn pawn)</c>, fired from its <c>Pawn_JobTracker.StartJob</c>
    /// prefix. After a crafter finishes a bill, it hands the just-made products to a haul WorkGiver so the
    /// CRAFTER hauls them to storage. Verified by reflection against Common Sense (workshop 1561769193,
    /// 1.6): it references no store-mode API at all, so it hauls regardless of the bill's store mode —
    /// which means our store-mode override alone (drop on floor) would simply be undone by it.
    ///
    /// We dynamically prefix Hauling_Opportunity and return "no opportunity" when the finished bill belongs
    /// to a bench set to Drop-at-bench, leaving the crafter free so a dedicated hauler moves the product
    /// later. Every other bench — and all of Common Sense's other behaviour — is untouched. Pick Up And
    /// Haul needs no such handling: it is a hauling WorkGiver (no post-bill crafter haul of its own), so it
    /// simply lets dedicated haulers bulk-move the dropped products, which is exactly what we want.
    ///
    /// All reflection + try/catch: if Common Sense is absent or renames the method, this no-ops and logs.
    /// </summary>
    public static class CommonSenseCompat
    {
        /// <summary>True when Common Sense was found and the suppression patch was applied.</summary>
        public static bool Active { get; private set; }

        public static void Apply(Harmony harmony)
        {
            try
            {
                Type ot = AccessTools.TypeByName("CommonSense.OpportunisticTasks");
                if (ot == null)
                    return; // Common Sense not installed -> nothing to do.

                MethodInfo target = AccessTools.Method(ot, "Hauling_Opportunity",
                    new[] { typeof(Job), typeof(Pawn) });
                if (target == null)
                {
                    Log.Warning("[WorkbenchIngredients] Common Sense is present but "
                        + "OpportunisticTasks.Hauling_Opportunity(Job, Pawn) was not found — 'Drop at bench' "
                        + "may be overridden by its product hauling. Please report your Common Sense version.");
                    return;
                }

                var prefix = new HarmonyMethod(typeof(CommonSenseCompat)
                    .GetMethod(nameof(SuppressForDropAtBench), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(target, prefix: prefix);
                Active = true;
                Log.Message("[WorkbenchIngredients] Common Sense detected — its opportunistic product-haul is "
                    + "suppressed on benches set to 'Drop at bench'; other benches are unaffected.");
            }
            catch (Exception e)
            {
                Log.Warning("[WorkbenchIngredients] Failed to apply Common Sense compat (harmless; its product "
                    + "hauling is left as-is). " + e);
            }
        }

        /// <summary>
        /// Prefix on <c>CommonSense.OpportunisticTasks.Hauling_Opportunity</c>. The parameter name
        /// <paramref name="billJob"/> matches the original so Harmony injects it. When the finished bill's
        /// bench is in Drop-at-bench mode we cancel the opportunity (null result, skip the original); every
        /// other case runs Common Sense unchanged.
        /// </summary>
        private static bool SuppressForDropAtBench(Job billJob, ref Job __result)
        {
            var comp = (((billJob?.bill) as Bill_Production)?.billStack?.billGiver as ThingWithComps)
                ?.GetComp<CompWorkbenchIngredients>();

            if (comp != null && comp.outputMode == WorkbenchOutputMode.Local)
            {
                __result = null; // no opportunistic product-haul for Drop-at-bench benches
                return false;    // skip Common Sense's Hauling_Opportunity
            }
            return true;
        }
    }
}
