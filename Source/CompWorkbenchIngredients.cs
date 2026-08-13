using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorkbenchIngredients
{
    /// <summary>Comp properties. Carries no config of its own — every selection lives on the comp instance,
    /// per work table. Added to work-table defs by the Patches XML (and WorkbenchCompInjector for subclasses).</summary>
    public class CompProperties_WorkbenchIngredients : CompProperties
    {
        public CompProperties_WorkbenchIngredients()
        {
            compClass = typeof(CompWorkbenchIngredients);
        }
    }

    /// <summary>
    /// Per-bench product-output routing mode. Only <see cref="Vanilla"/> and <see cref="Local"/> are
    /// implemented; PrimarySecondary and EvenDistribution are planned (see README) and intentionally
    /// not offered in the UI yet.
    /// </summary>
    public enum WorkbenchOutputMode
    {
        /// <summary>Leave every bill's own store mode alone (default).</summary>
        Vanilla,
        /// <summary>Force "drop at bench" for every bill so the crafter never hauls the product;
        /// a hauler moves it to storage later.</summary>
        Local,
    }

    /// <summary>
    /// Per-bench ingredient-source configuration. Stores the selected source locations (a UNION of any
    /// number of stockpile zones, storage buildings and storage groups) plus a fallback search radius
    /// used only when nothing is selected. Persisted in the save via <see cref="PostExposeData"/>.
    /// </summary>
    public class CompWorkbenchIngredients : ThingComp
    {
        // The three kinds of selectable source. All are ILoadReferenceable (Zone / Thing / StorageGroup),
        // so they scribe as references and resolve back to the live objects on load.
        public List<Zone_Stockpile> sourceZones = new List<Zone_Stockpile>();
        public List<Building_Storage> sourceBuildings = new List<Building_Storage>();
        public List<StorageGroup> sourceGroups = new List<StorageGroup>();

        // Per-bench product-output routing. Vanilla leaves each bill's store mode untouched.
        public WorkbenchOutputMode outputMode = WorkbenchOutputMode.Vanilla;

        // Per-bench fallback radius. -1 is the "not set yet" sentinel: the first read seeds it from the
        // global default so a brand-new bench inherits the mod-setting value.
        private float fallbackRadius = -1f;

        public float FallbackRadius
        {
            get
            {
                if (fallbackRadius < 0f)
                    fallbackRadius = WorkbenchIngredientsMod.Settings.defaultRadius;
                return fallbackRadius;
            }
            set => fallbackRadius = value;
        }

        /// <summary>True when at least one source of any kind is selected — the map-wide sourced path applies.</summary>
        public bool HasAnySource =>
            (sourceZones != null && sourceZones.Count > 0) ||
            (sourceBuildings != null && sourceBuildings.Count > 0) ||
            (sourceGroups != null && sourceGroups.Count > 0);

        public override void PostExposeData()
        {
            base.PostExposeData();

            // Scribe the selected sources as REFERENCES (LookMode.Reference), not deep-saved objects: the
            // zones/buildings/groups are owned by the map, we only remember which ones this bench points at.
            // On load a reference whose target no longer exists resolves to null; we prune those below so a
            // deleted stockpile/shelf/group simply drops out of the selection.
            Scribe_Collections.Look(ref sourceZones, "sourceZones", LookMode.Reference);
            Scribe_Collections.Look(ref sourceBuildings, "sourceBuildings", LookMode.Reference);
            Scribe_Collections.Look(ref sourceGroups, "sourceGroups", LookMode.Reference);
            Scribe_Values.Look(ref fallbackRadius, "fallbackRadius", -1f);
            Scribe_Values.Look(ref outputMode, "outputMode", WorkbenchOutputMode.Vanilla);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (sourceZones == null) sourceZones = new List<Zone_Stockpile>();
                if (sourceBuildings == null) sourceBuildings = new List<Building_Storage>();
                if (sourceGroups == null) sourceGroups = new List<StorageGroup>();

                // Drop references to sources deleted while this comp's data persisted.
                sourceZones.RemoveAll(z => z == null);
                sourceBuildings.RemoveAll(b => b == null || b.Destroyed);
                sourceGroups.RemoveAll(g => g == null);
            }
        }

        /// <summary>
        /// All things currently held by the selected sources, map-wide and de-duplicated. A thing reachable
        /// through more than one selected source (e.g. a shelf that is also in a selected group) is yielded
        /// once. This is the candidate set handed to the vanilla ingredient matcher; filtering/counts happen
        /// there, not here.
        /// </summary>
        public IEnumerable<Thing> EnumerateSourceThings()
        {
            var seen = new HashSet<Thing>();

            if (sourceZones != null)
                foreach (Zone_Stockpile z in sourceZones)
                {
                    SlotGroup sg = z?.GetSlotGroup();
                    if (sg == null) continue;
                    foreach (Thing t in sg.HeldThings)
                        if (seen.Add(t)) yield return t;
                }

            if (sourceBuildings != null)
                foreach (Building_Storage b in sourceBuildings)
                {
                    if (b == null || !b.Spawned) continue;
                    SlotGroup sg = b.GetSlotGroup();
                    if (sg == null) continue;
                    foreach (Thing t in sg.HeldThings)
                        if (seen.Add(t)) yield return t;
                }

            if (sourceGroups != null)
                foreach (StorageGroup g in sourceGroups)
                {
                    if (g == null) continue;
                    foreach (Thing t in g.HeldThings)
                        if (seen.Add(t)) yield return t;
                }
        }
    }
}
