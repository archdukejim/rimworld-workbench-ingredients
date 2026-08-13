using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkbenchIngredients
{
    /// <summary>
    /// Per-bench product-output override. When a bench is set to <see cref="WorkbenchOutputMode.Local"/>,
    /// every bill at that bench stores its product with "drop at bench" — the crafter drops it in place and
    /// returns to work instead of hauling it to a stockpile; a hauler relocates it later. This is the output
    /// mirror of the ingredient-sources feature: it overrides each bill's own store setting, per bench.
    ///
    /// PATCH TARGET (VERIFIED against RimWorld 1.6 Assembly-CSharp via reflection):
    ///     public BillStoreModeDef Bill_Production.GetStoreMode()
    ///   This is the single method the recipe-finishing toil reads to decide whether the worker drops the
    ///   product (DropOnFloor) or carries it to a stockpile (BestStockpile / SpecificStockpile), so a
    ///   postfix here reroutes storage for every bill without touching the storing logic itself.
    ///   BillStoreModeDefOf.DropOnFloor is a vanilla DefOf, populated at game load.
    /// </summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.GetStoreMode))]
    public static class Patch_Bill_Production_GetStoreMode
    {
        static void Postfix(Bill_Production __instance, ref BillStoreModeDef __result)
        {
            // Reach the bench that owns this bill and check its per-bench output mode.
            var comp = (__instance.billStack?.billGiver as ThingWithComps)?.GetComp<CompWorkbenchIngredients>();
            if (comp == null || comp.outputMode != WorkbenchOutputMode.Local)
                return; // not our bench, or bench left on vanilla routing -> leave the bill's store mode alone

            // Local mode: force drop-at-bench. Dropping on the floor always succeeds, so no fallback is
            // needed; the product simply waits at the bench for a hauler.
            __result = BillStoreModeDefOf.DropOnFloor;
        }
    }
}
