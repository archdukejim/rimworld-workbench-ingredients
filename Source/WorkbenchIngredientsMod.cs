using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WorkbenchIngredients
{
    /// <summary>
    /// Mod entry point. Constructed once when mods load; applies the Harmony patches and owns the
    /// global settings (the default fallback radius that new benches inherit).
    /// </summary>
    public class WorkbenchIngredientsMod : Mod
    {
        public static WorkbenchIngredientsSettings Settings { get; private set; }

        public WorkbenchIngredientsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WorkbenchIngredientsSettings>();

            // Mod ctors run after every mod's assemblies are loaded, so soft-dependency types (Common
            // Sense) already exist here if the player has them active. Our About.xml loadAfter them, so
            // their Harmony patches are in place before we wrap Common Sense's opportunistic product-haul.
            var harmony = new Harmony("archdukejim.workbenchingredients");
            harmony.PatchAll();
            CommonSenseCompat.Apply(harmony);
        }

        public override string SettingsCategory() => "WI.SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
    }

    /// <summary>
    /// Global mod settings. Only holds the default fallback radius used to pre-fill a brand-new bench's
    /// per-bench fallback slider. Stored in the mod config file, never in a save.
    /// </summary>
    public class WorkbenchIngredientsSettings : ModSettings
    {
        /// <summary>Default per-bench fallback radius, used when a bench has no source selected and has not
        /// been given its own radius yet. A deliberately conservative default: vanilla bills default to 999
        /// (effectively map-wide), but 25 keeps a new bench searching locally until the player widens it.</summary>
        public float defaultRadius = 25f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref defaultRadius, "defaultRadius", 25f);
        }

        public void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("WI.DefaultRadius".Translate(defaultRadius.ToString("F0")));
            list.Gap(2f);
            list.Label("WI.DefaultRadius.Desc".Translate(), -1f, null);
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
    public static class WorkbenchCompInjector
    {
        static WorkbenchCompInjector()
        {
            int added = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass == null) continue;
                if (!typeof(RimWorld.Building_WorkTable).IsAssignableFrom(def.thingClass)) continue;

                if (def.comps == null) def.comps = new List<CompProperties>();
                if (def.comps.Any(c => c is CompProperties_WorkbenchIngredients)) continue; // already added by XML

                def.comps.Add(new CompProperties_WorkbenchIngredients());
                added++;
            }

            if (added > 0)
                Log.Message($"[WorkbenchIngredients] Added ingredient-source comp to {added} work-table " +
                            "subclass def(s) not covered by the XML patch.");
        }
    }
}
