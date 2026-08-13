using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace BenchIngredientSources
{
    /// <summary>
    /// Mod entry point. Constructed once when mods load; applies the Harmony patches and owns the
    /// global settings (the default fallback radius that new benches inherit).
    /// </summary>
    public class BenchIngredientSourcesMod : Mod
    {
        public static BenchIngredientSourcesSettings Settings { get; private set; }

        public BenchIngredientSourcesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BenchIngredientSourcesSettings>();
            new Harmony("archdukejim.benchingredientsources").PatchAll();
        }

        public override string SettingsCategory() => "BIS.SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
    }

    /// <summary>
    /// Global mod settings. Only holds the default fallback radius used to pre-fill a brand-new bench's
    /// per-bench fallback slider. Stored in the mod config file, never in a save.
    /// </summary>
    public class BenchIngredientSourcesSettings : ModSettings
    {
        /// <summary>Default per-bench fallback radius, used when a bench has no source selected and has
        /// not been given its own radius yet. Matches the vanilla bill default of 999 = effectively map-wide.</summary>
        public float defaultRadius = 25f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref defaultRadius, "defaultRadius", 25f);
        }

        public void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("BIS.DefaultRadius".Translate(defaultRadius.ToString("F0")));
            list.Gap(2f);
            list.Label("BIS.DefaultRadius.Desc".Translate(), -1f, null);
            defaultRadius = Mathf.Round(list.Slider(defaultRadius, Constants.MinRadius, Constants.MaxRadius));
            list.End();
        }
    }

    /// <summary>Shared tunables so the settings slider and the per-bench slider agree on their range.</summary>
    public static class Constants
    {
        public const float MinRadius = 3f;
        public const float MaxRadius = 100f;
    }

    /// <summary>
    /// Runtime safety net for the XML patch. The Patches XML can only string-match ThingDefs whose
    /// thingClass is literally "Building_WorkTable", so modded benches that use a SUBCLASS of
    /// Building_WorkTable are missed. This walks every ThingDef after all defs are loaded and adds the
    /// comp to any Building_WorkTable subclass that doesn't already have it (the XML-matched ones do,
    /// so they're skipped here). Runs at [StaticConstructorOnStartup] — after defs load, before any map
    /// with work tables is spawned — so the comp instance is created for every work table on load.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BenchCompInjector
    {
        static BenchCompInjector()
        {
            int added = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass == null) continue;
                if (!typeof(RimWorld.Building_WorkTable).IsAssignableFrom(def.thingClass)) continue;

                if (def.comps == null) def.comps = new List<CompProperties>();
                if (def.comps.Any(c => c is CompProperties_BenchIngredientSources)) continue; // already added by XML

                def.comps.Add(new CompProperties_BenchIngredientSources());
                added++;
            }

            if (added > 0)
                Log.Message($"[BenchIngredientSources] Added ingredient-source comp to {added} work-table " +
                            "subclass def(s) not covered by the XML patch.");
        }
    }
}
