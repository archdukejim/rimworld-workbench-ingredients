using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkbenchIngredients
{
    /// <summary>
    /// Per-bench configuration window. Shows a scrollable checklist of the map's stockpile zones, storage
    /// buildings and storage groups (each a selectable source), plus the fallback default-radius slider used
    /// when nothing is selected. Edits write straight back to the bench's comp.
    /// </summary>
    public class Window_WorkbenchIngredients : Window
    {
        private readonly CompWorkbenchIngredients comp;
        private Vector2 scrollPos;

        public Window_WorkbenchIngredients(CompWorkbenchIngredients comp)
        {
            this.comp = comp;
            doCloseX = true;
            closeOnClickedOutside = true;
            draggable = true;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(520f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            Map map = comp?.parent?.Map;
            if (map == null) { Close(); return; }

            float y = 0f;

            // Title.
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 34f), "WI.WindowTitle".Translate(comp.parent.LabelCap));
            Text.Font = GameFont.Small;
            y += 42f;

            // ===== OUTPUT: where finished products go, per bench (overrides each bill's store setting). =====
            DimHeader(new Rect(0f, y, inRect.width, 22f), "WI.OutputHeader".Translate());
            y += 24f;
            Widgets.DrawLineHorizontal(0f, y, inRect.width);
            y += 6f;

            var rVanilla = new Rect(0f, y, inRect.width, 24f);
            if (Widgets.RadioButtonLabeled(rVanilla, "WI.OutputMode.Vanilla".Translate(),
                    comp.outputMode == WorkbenchOutputMode.Vanilla))
                comp.outputMode = WorkbenchOutputMode.Vanilla;
            TooltipHandler.TipRegion(rVanilla, "WI.OutputMode.Vanilla.Desc".Translate());
            y += 26f;

            var rLocal = new Rect(0f, y, inRect.width, 24f);
            if (Widgets.RadioButtonLabeled(rLocal, "WI.OutputMode.Local".Translate(),
                    comp.outputMode == WorkbenchOutputMode.Local))
                comp.outputMode = WorkbenchOutputMode.Local;
            TooltipHandler.TipRegion(rLocal, "WI.OutputMode.Local.Desc".Translate());
            y += 28f;

            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Widgets.Label(new Rect(0f, y, inRect.width, 34f), "WI.OutputComingSoon".Translate());
            GUI.color = Color.white;
            y += 40f;

            // ===== INGREDIENT SOURCES: where ingredients come from, per bench. =====
            DimHeader(new Rect(0f, y, inRect.width - 140f, 22f), "WI.SourcesHeader".Translate());
            if (comp.HasAnySource &&
                Widgets.ButtonText(new Rect(inRect.width - 130f, y - 2f, 130f, 24f), "WI.ClearSelection".Translate()))
            {
                comp.sourceZones.Clear();
                comp.sourceBuildings.Clear();
                comp.sourceGroups.Clear();
            }
            y += 24f;
            Widgets.DrawLineHorizontal(0f, y, inRect.width);
            y += 6f;

            Widgets.Label(new Rect(0f, y, inRect.width, 40f),
                comp.HasAnySource ? "WI.StatusSourced".Translate() : "WI.StatusRadius".Translate());
            y += 44f;

            // Gather the three source kinds from the map.
            List<Zone_Stockpile> zones = map.zoneManager.AllZones.OfType<Zone_Stockpile>()
                .OrderBy(z => z.label).ToList();
            List<Building_Storage> buildings = map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>()
                .OrderBy(b => b.LabelCap.ToString()).ToList();
            List<StorageGroup> groups = map.storageGroups?.StorageGroupsForReading != null
                ? map.storageGroups.StorageGroupsForReading.OrderBy(g => g.GroupingLabel).ToList()
                : new List<StorageGroup>();

            // Scroll list of sources, fallback-radius slider pinned to the bottom.
            const float bottomH = 74f;
            var outRect = new Rect(0f, y, inRect.width, inRect.height - y - bottomH - 8f);

            const float rowH = 26f;
            const float headerH = 30f;
            int rows = zones.Count + buildings.Count + groups.Count;
            float viewH = 3f * headerH + rows * rowH + 3f * 8f + 40f; // 3 section headers + rows + gaps + empty-notes slack
            var viewRect = new Rect(0f, 0f, outRect.width - 18f, Mathf.Max(viewH, outRect.height));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            var list = new Listing_Standard();
            list.Begin(viewRect);

            DrawZoneSection(list, "WI.SectionZones".Translate(), zones);
            DrawBuildingSection(list, "WI.SectionBuildings".Translate(), buildings);
            DrawGroupSection(list, "WI.SectionGroups".Translate(), groups);

            list.End();
            Widgets.EndScrollView();

            // Fallback radius (always editable; only takes effect when nothing is selected).
            var bottom = new Rect(0f, inRect.height - bottomH, inRect.width, bottomH);
            var bl = new Listing_Standard();
            bl.Begin(bottom);
            GUI.color = comp.HasAnySource ? new Color(1f, 1f, 1f, 0.5f) : Color.white; // dim when unused
            bl.Label("WI.FallbackRadius".Translate(comp.FallbackRadius.ToString("F0")));
            comp.FallbackRadius = Mathf.Round(bl.Slider(comp.FallbackRadius, Constants.MinRadius, Constants.MaxRadius));
            GUI.color = Color.white;
            bl.End();
        }

        /// <summary>A dim, bottom-aligned section header.</summary>
        private static void DimHeader(Rect rect, string label)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prev;
        }

        private void DrawZoneSection(Listing_Standard list, string header, List<Zone_Stockpile> zones)
        {
            SectionHeader(list, header);
            if (zones.Count == 0) { list.Label("WI.None".Translate()); return; }
            foreach (Zone_Stockpile z in zones)
            {
                bool sel = comp.sourceZones.Contains(z);
                bool cur = sel;
                list.CheckboxLabeled(z.label, ref cur);
                if (cur != sel) Toggle(comp.sourceZones, z, cur);
            }
        }

        private void DrawBuildingSection(Listing_Standard list, string header, List<Building_Storage> buildings)
        {
            SectionHeader(list, header);
            if (buildings.Count == 0) { list.Label("WI.None".Translate()); return; }
            foreach (Building_Storage b in buildings)
            {
                bool sel = comp.sourceBuildings.Contains(b);
                bool cur = sel;
                list.CheckboxLabeled(b.LabelCap, ref cur);
                if (cur != sel) Toggle(comp.sourceBuildings, b, cur);
            }
        }

        private void DrawGroupSection(Listing_Standard list, string header, List<StorageGroup> groups)
        {
            SectionHeader(list, header);
            if (groups.Count == 0) { list.Label("WI.None".Translate()); return; }
            foreach (StorageGroup g in groups)
            {
                bool sel = comp.sourceGroups.Contains(g);
                bool cur = sel;
                list.CheckboxLabeled(g.GroupingLabel, ref cur);
                if (cur != sel) Toggle(comp.sourceGroups, g, cur);
            }
        }

        private static void SectionHeader(Listing_Standard list, string label)
        {
            list.Gap(4f);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            list.Label(label);
            GUI.color = Color.white;
            list.GapLine(2f);
        }

        private static void Toggle<T>(List<T> selection, T item, bool add)
        {
            if (add) { if (!selection.Contains(item)) selection.Add(item); }
            else selection.Remove(item);
        }
    }
}
