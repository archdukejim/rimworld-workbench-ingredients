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

            // Title.
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, 0f, inRect.width, 34f);
            Widgets.Label(titleRect, "WI.WindowTitle".Translate(comp.parent.LabelCap));
            Text.Font = GameFont.Small;

            // Status line: which mode this bench is in right now.
            var statusRect = new Rect(0f, 36f, inRect.width, 40f);
            Widgets.Label(statusRect, comp.HasAnySource
                ? "WI.StatusSourced".Translate()
                : "WI.StatusRadius".Translate());

            // "Clear selection" convenience button, top-right.
            var clearRect = new Rect(inRect.width - 130f, 4f, 130f, 26f);
            if (comp.HasAnySource && Widgets.ButtonText(clearRect, "WI.ClearSelection".Translate()))
            {
                comp.sourceZones.Clear();
                comp.sourceBuildings.Clear();
                comp.sourceGroups.Clear();
            }

            // Gather the three source kinds from the map.
            List<Zone_Stockpile> zones = map.zoneManager.AllZones.OfType<Zone_Stockpile>()
                .OrderBy(z => z.label).ToList();
            List<Building_Storage> buildings = map.listerBuildings.allBuildingsColonist.OfType<Building_Storage>()
                .OrderBy(b => b.LabelCap.ToString()).ToList();
            List<StorageGroup> groups = map.storageGroups?.StorageGroupsForReading != null
                ? map.storageGroups.StorageGroupsForReading.OrderBy(g => g.GroupingLabel).ToList()
                : new List<StorageGroup>();

            // Layout: scroll list in the middle, fallback-radius slider pinned to the bottom.
            const float bottomH = 74f;
            float top = 80f;
            var outRect = new Rect(0f, top, inRect.width, inRect.height - top - bottomH - 8f);

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
