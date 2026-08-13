using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkbenchIngredients
{
    /// <summary>
    /// The heart of the mod. Redirects a bill's ingredient search based on the bench's comp:
    ///
    ///   - Sources selected -> build the candidate set from the checked zones/shelves/groups (map-wide)
    ///     and hand it straight to the vanilla set-matcher, bypassing the vanilla radius/region scan.
    ///   - No sources selected -> let the entire vanilla search run, but temporarily replace the bill's
    ///     own ingredientSearchRadius with the per-bench fallback radius (restored in the postfix).
    ///
    /// Only the SET of candidate things changes. Filtering, required counts, allow-mixing and closest-pick
    /// are all still done by vanilla (WorkGiver_DoBill.TryFindBestBillIngredientsInSet, see IngredientSetMatcher).
    ///
    /// PATCH TARGET (VERIFIED against RimWorld 1.6 Assembly-CSharp via reflection):
    ///     private static bool WorkGiver_DoBill.TryFindBestBillIngredients(
    ///         Bill bill, Pawn pawn, Thing billGiver, List&lt;ThingCount&gt; chosen,
    ///         List&lt;IngredientCount&gt; missingIngredients)
    ///   This is the single entry point WorkGiver_DoBill.JobOnThing uses to resolve a bill's ingredients.
    ///   It is targeted by NAME only (see TargetMethod), so a parameter change on another version doesn't
    ///   break the patch; if the name is ever overloaded, Harmony throws at startup and this would need an
    ///   explicit argumentTypes list. Believed identical on 1.5.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients
    {
        // Reused candidate buffer. The bill-ingredient search runs on the main thread only, same as vanilla's
        // own static buffers, so a single shared list is safe and avoids per-call allocation.
        private static readonly List<Thing> candidates = new List<Thing>();

        private static bool degradeWarned;

        // Sentinel meaning "prefix did not override the radius, postfix must not restore".
        private const float NoOverride = float.NaN;

        static MethodBase TargetMethod()
        {
            // Resolve by name; tolerate the version-specific parameter list.
            MethodInfo m = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients");
            if (m == null)
                Log.Error("[WorkbenchIngredients] Could not find WorkGiver_DoBill.TryFindBestBillIngredients " +
                          "to patch — the mod's core feature will not work. Please report your RimWorld version.");
            return m;
        }

        // Parameter names match the original method exactly so Harmony injects them (chosen and
        // missingIngredients are the caller's output lists — we fill them just like vanilla would).
        static bool Prefix(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen,
                           List<IngredientCount> missingIngredients, ref bool __result, ref float __state)
        {
            __state = NoOverride;

            CompWorkbenchIngredients comp = (billGiver as ThingWithComps)?.GetComp<CompWorkbenchIngredients>();
            if (comp == null) return true; // not one of our benches -> vanilla, untouched

            if (comp.HasAnySource)
            {
                if (IngredientSetMatcher.Supported)
                {
                    // SELECTED-SOURCES PATH: search only the checked sources, map-wide. Skip the original.
                    __result = TryFindFromSources(bill, pawn, billGiver, chosen, missingIngredients, comp);
                    return false;
                }

                // The internal matcher couldn't be resolved on this version, so we can't safely restrict to
                // the selected sources. Degrade to a map-wide radius search (vanilla still finds ingredients,
                // just not source-restricted) rather than breaking crafting. Warn once.
                if (!degradeWarned)
                {
                    degradeWarned = true;
                    Log.Warning("[WorkbenchIngredients] Ingredient set-matcher unavailable; benches with " +
                                "selected sources fall back to a map-wide radius search this session.");
                }
                __state = bill.ingredientSearchRadius;
                bill.ingredientSearchRadius = 9999f;
                return true;
            }

            // FALLBACK-RADIUS PATH: no source selected. Run the full vanilla search but with the bench's
            // fallback radius substituted for the bill's own, then restore it in the postfix.
            __state = bill.ingredientSearchRadius;
            bill.ingredientSearchRadius = comp.FallbackRadius;
            return true;
        }

        static void Postfix(Bill bill, float __state)
        {
            // Restore the bill's real radius only if the prefix overrode it (NaN sentinel = untouched).
            if (!float.IsNaN(__state))
                bill.ingredientSearchRadius = __state;
        }

        /// <summary>
        /// Build the candidate list from the bench's selected sources, applying the same per-thing gate the
        /// vanilla region scan applies (allowed by the bill's ingredient filter, reachable, reservable, not
        /// forbidden), then let the vanilla matcher choose the actual combination into <paramref name="chosen"/>.
        /// </summary>
        static bool TryFindFromSources(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen,
                                       List<IngredientCount> missingIngredients, CompWorkbenchIngredients comp)
        {
            chosen.Clear();
            candidates.Clear();

            Map map = pawn.Map;
            foreach (Thing t in comp.EnumerateSourceThings())
            {
                if (t == null || !t.Spawned || t.Map != map) continue;
                // Reuse the bill's own filter check — we do NOT reimplement ingredient filtering.
                if (!bill.IsFixedOrAllowedIngredient(t)) continue;
                if (t.IsForbidden(pawn)) continue;
                if (!pawn.CanReserve(t)) continue;
                if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly)) continue;
                candidates.Add(t);
            }

            // Hand the set to vanilla. alreadySorted:false lets vanilla sort by distance from the bench cell,
            // so the pawn still prefers the closest sufficient ingredients within the selected sources.
            bool found = IngredientSetMatcher.TryMatch(candidates, bill, chosen,
                                                       RootCell(billGiver, pawn), missingIngredients);

            candidates.Clear();
            return found;
        }

        /// <summary>The cell distances are measured from — the bench's interaction cell if it has one.</summary>
        static IntVec3 RootCell(Thing billGiver, Pawn pawn)
        {
            if (billGiver is Building b && b.def.hasInteractionCell)
                return b.InteractionCell;
            return billGiver.Position;
        }
    }

    /// <summary>
    /// Reflection wrapper around WorkGiver_DoBill's private set-matcher, which is the piece that turns a set
    /// of candidate things into the chosen ingredient stacks (enforcing counts, allow-mixing, closest pick).
    /// Reusing it is what keeps our behaviour identical to vanilla except for the candidate set.
    ///
    /// VERIFIED against RimWorld 1.6 Assembly-CSharp via reflection:
    ///   private static bool WorkGiver_DoBill.TryFindBestBillIngredientsInSet(
    ///       List&lt;Thing&gt; availableThings, Bill bill, List&lt;ThingCount&gt; chosen,
    ///       IntVec3 rootCell, bool alreadySorted, List&lt;IngredientCount&gt; missingIngredients)
    ///   (This dispatcher internally calls TryFindBestBillIngredientsInSet_NoMix / _AllowMix based on
    ///   recipe.allowMixingIngredients, so calling it reuses BOTH mixing strategies. Believed identical on 1.5.)
    ///
    /// Rather than hard-code the parameter order, we resolve the method by name and bind arguments BY TYPE, so
    /// minor reordering or an added/removed bool/list still works. If the method can't be resolved or bound,
    /// Supported is false and the caller degrades gracefully (see the prefix).
    /// </summary>
    internal static class IngredientSetMatcher
    {
        private static bool initialized;
        private static MethodInfo dispatcher;
        private static ParameterInfo[] pars;
        private static bool loggedRuntimeFail;

        public static bool Supported
        {
            get { EnsureInit(); return dispatcher != null; }
        }

        private static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;

            dispatcher = AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet");
            if (dispatcher == null)
            {
                Log.Warning("[WorkbenchIngredients] Could not resolve " +
                            "WorkGiver_DoBill.TryFindBestBillIngredientsInSet. Selected-source benches will " +
                            "degrade to a map-wide radius search. Please report your RimWorld version so the " +
                            "method name can be updated.");
                return;
            }

            pars = dispatcher.GetParameters();
            // Sanity-check: we can only bind the parameter types we know about.
            foreach (ParameterInfo p in pars)
            {
                if (!CanBind(p.ParameterType))
                {
                    Log.Warning("[WorkbenchIngredients] TryFindBestBillIngredientsInSet has an unexpected " +
                                $"parameter '{p.Name}' of type {p.ParameterType}. Falling back to a map-wide " +
                                "radius search for selected-source benches.");
                    dispatcher = null;
                    return;
                }
            }
        }

        private static bool CanBind(Type pt)
        {
            if (pt.IsByRef) pt = pt.GetElementType();
            return pt == typeof(List<Thing>)
                || pt == typeof(Bill)
                || pt == typeof(List<ThingCount>)
                || pt == typeof(IntVec3)
                || pt == typeof(bool)
                || pt == typeof(List<IngredientCount>);
        }

        public static bool TryMatch(List<Thing> available, Bill bill, List<ThingCount> chosen,
                                    IntVec3 rootCell, List<IngredientCount> missingIngredients)
        {
            EnsureInit();
            if (dispatcher == null) return false;
            if (missingIngredients == null) missingIngredients = new List<IngredientCount>();

            object[] args = new object[pars.Length];
            for (int i = 0; i < pars.Length; i++)
            {
                Type pt = pars[i].ParameterType;
                if (pt.IsByRef) pt = pt.GetElementType();

                if (pt == typeof(List<Thing>)) args[i] = available;
                else if (pt == typeof(Bill)) args[i] = bill;
                else if (pt == typeof(List<ThingCount>)) args[i] = chosen;
                else if (pt == typeof(IntVec3)) args[i] = rootCell;
                else if (pt == typeof(bool)) args[i] = false;                        // alreadySorted = false
                else if (pt == typeof(List<IngredientCount>)) args[i] = missingIngredients; // caller's output list
                else return false;                                                   // guarded by CanBind, defensive
            }

            try
            {
                return (bool)dispatcher.Invoke(null, args);
            }
            catch (Exception e)
            {
                if (!loggedRuntimeFail)
                {
                    loggedRuntimeFail = true;
                    Log.Error("[WorkbenchIngredients] Invoking TryFindBestBillIngredientsInSet threw; " +
                              "selected-source benches will report no ingredients. " + e);
                }
                return false;
            }
        }
    }
}
