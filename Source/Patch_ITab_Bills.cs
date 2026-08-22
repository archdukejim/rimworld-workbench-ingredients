using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkbenchIngredients
{
    /// <summary>
    /// Adds a "Sources / output" button to the header row of the Bills tab. Clicking it opens the
    /// per-bench configuration window (ingredient sources + product output) for the selected work table.
    ///
    /// PATCH TARGET (NEEDS-VERIFICATION): protected override void RimWorld.ITab_Bills.FillTab().
    /// FillTab draws in a GUI group whose origin is the tab's top-left, so a postfix draws in the same
    /// local coordinate space. The button rect is placed just right of the vanilla "Add bill" dropdown
    /// (leftmost in the header) and left of the copy/paste buttons (rightmost); nudge the constants below
    /// if a future layout change moves those. Verify placement in-game.
    /// </summary>
    [HarmonyPatch(typeof(ITab_Bills), "FillTab")]
    public static class Patch_ITab_Bills_FillTab
    {
        // Cached reflected access to the protected size field (declared on InspectTabBase); walks base types.
        private static FieldInfo sizeField;

        static void Postfix(ITab __instance)
        {
            // The selected building is the bench. Only draw for work tables that carry our comp.
            var comp = (Find.Selector.SingleSelectedThing as ThingWithComps)?.GetComp<CompWorkbenchIngredients>();
            if (comp == null) return;

            Vector2 winSize = TabSize(__instance);

            const float inset = 10f;         // ITab_Bills contracts its content by ~10px
            const float addBillWidth = 150f; // vanilla "Add bill" dropdown width
            const float gap = 6f;
            const float maxBtnWidth = 160f;
            const float minBtnWidth = 48f;   // below this the header row is too cramped; drop to a 2nd row
            const float btnHeight = 29f;

            // The header row's free span runs from just right of the Add-bill dropdown to just left of the
            // copy/paste column vanilla pins to the right edge (CopyPasteUI.CopyPasteColumnWidth wide). Bound
            // against that real constant instead of a magic number so a wider copy/paste column still clears.
            float x0 = inset + addBillWidth + gap;
            float xMax = winSize.x - inset - CopyPasteUI.CopyPasteColumnWidth - gap;
            float avail = xMax - x0;

            Rect rect;
            if (avail >= minBtnWidth)
            {
                // Fits beside "Add bill": shrink to the gap rather than overflow into the copy/paste buttons.
                rect = new Rect(x0, inset, Mathf.Min(maxBtnWidth, avail), btnHeight);
            }
            else
            {
                // Pathological layout (very narrow tab / a wide localised Add-bill label): no room on the
                // header row. Fall back to a full-width row just below it so the button is never lost —
                // previously it was simply skipped and the whole feature became unreachable for this bench.
                rect = new Rect(inset, inset + btnHeight + 4f, winSize.x - 2f * inset, btnHeight);
            }

            // Tooltip carries the full purpose, so even a shrunk/truncated button stays understandable.
            TooltipHandler.TipRegion(rect, "WI.Button.Tip".Translate());
            if (Widgets.ButtonText(rect, "WI.Button".Translate()))
                Find.WindowStack.Add(new Window_WorkbenchIngredients(comp));
        }

        static Vector2 TabSize(ITab instance)
        {
            if (sizeField == null)
                sizeField = AccessTools.Field(instance.GetType(), "size");
            if (sizeField != null)
                return (Vector2)sizeField.GetValue(instance);
            return new Vector2(420f, 480f); // vanilla ITab_Bills default, if the field ever moves
        }
    }
}
