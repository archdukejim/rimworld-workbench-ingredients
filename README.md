# Workbench Ingredients

A RimWorld 1.5/1.6 mod that moves crafting ingredient-sourcing from **per-bill** to **per-workbench**. (packageId `archdukejim.WorkbenchIngredients`)

Each work table gets a **Sources / output** button in its Bills tab, opening one per-bench window with two sections.

### Ingredient sources — where ingredients come from

Check any combination of:

- stockpile **zones**
- storage **buildings** (shelves/racks)
- storage **groups**

The bench draws ingredients from the **union** of everything checked, **map-wide**, and ignores every bill's own ingredient search radius.

If nothing is checked, the bench falls back to a single **per-bench radius** (pre-filled from a global default in mod settings), applied to all bills at that bench — overriding each bill's own radius.

Only *where* ingredients are searched changes. The ingredient filter, required counts, allow-mixing, reachability and forbidden checks are all vanilla: the mod reuses the game's own ingredient matcher and only swaps the candidate set.

### Output — where finished products go

A per-bench mode that overrides each bill's store setting:

- **Vanilla** *(default)* — each bill keeps its own store setting.
- **Drop at bench** — the crafter drops the finished product at the bench and goes straight back to work; a hauler moves it to storage later, so skilled crafters never waste time hauling.

Planned modes (not yet implemented): **Primary/Secondary** (fill a primary destination, e.g. medkits → hospital, then overflow to bulk) and **Even distribution** (top up all eligible stockpiles to balance stacks). These are storage-routing behaviours and will likely be spec'd as their own system.

## How it works

| Piece | File |
|---|---|
| Comp added to every work table (stores the per-bench selection + fallback radius) | `Source/CompWorkbenchIngredients.cs` |
| XML that attaches the comp to `Building_WorkTable` defs | `Patches/AddWorkbenchIngredientsComp.xml` |
| Runtime net that also attaches it to `Building_WorkTable` *subclasses* | `WorkbenchCompInjector` in `Source/WorkbenchIngredientsMod.cs` |
| Ingredient-search override (both selected-sources and fallback-radius paths) | `Source/Patch_WorkGiver_DoBill.cs` |
| Output override (Local mode → drop at bench) | `Source/Patch_Bill_GetStoreMode.cs` |
| Common Sense soft-compat (suppress its product-haul on Drop-at-bench benches) | `Source/CommonSenseCompat.cs` |
| "Sources / output" tab button | `Source/Patch_ITab_Bills.cs` |
| Combined per-bench window (output mode + sources checklist + fallback slider) | `Source/Window_WorkbenchIngredients.cs` |
| Global default-radius setting | `WorkbenchIngredientsSettings` in `Source/WorkbenchIngredientsMod.cs` |

The ingredient override patches the private `WorkGiver_DoBill.TryFindBestBillIngredients` and, for selected sources, calls the private set-matcher `WorkGiver_DoBill.TryFindBestBillIngredientsInSet` by reflection (resolved by name, arguments bound by type) so it degrades gracefully if a future version renames it. Both signatures were verified against 1.6 `Assembly-CSharp.dll`.

The output override is a postfix on the public `Bill_Production.GetStoreMode()`: in **Drop at bench** mode it returns `BillStoreModeDefOf.DropOnFloor`, so the vanilla recipe-finishing toil simply drops the product in place instead of making the crafter carry it. All the storing logic stays vanilla.

## Compatibility

Both are soft dependencies (detected by reflection, never referenced at compile time); the mod runs fine without them. It `loadAfter` both.

- **Pick Up And Haul** — no handling needed. It's a hauling WorkGiver, so it doesn't send crafters hauling their own output; it just lets dedicated haulers *bulk*-move the products dropped by **Drop at bench**. Complementary.
- **Common Sense** — its "haul products over bills" is an opportunistic task (`OpportunisticTasks.Hauling_Opportunity`) that, after a bill, hands the products to a haul WorkGiver so the *crafter* hauls them. It ignores store mode, so it would undo **Drop at bench**. `CommonSenseCompat` dynamically prefixes that method and cancels the opportunity **only** for benches set to Drop-at-bench — every other bench, and all of Common Sense's other behaviour, is untouched. (Its `adv_haul_all_ings` ingredient detour is a separate feature, unrelated to output.)

## Building

```bash
dotnet build Source/WorkbenchIngredients.csproj -c Release
```

Output goes to `Assemblies/WorkbenchIngredients.dll`. Adjust `RimWorldPath` / the Harmony `HintPath` in the `.csproj` if your install differs.

## Keep it loaded

The mod attaches a component to every work-table def to store per-bench selections. Removing the mod is safe (benches revert to vanilla per-bill radius) but discards every bench's configuration and logs one-time "could not load" warnings, so keep it enabled while a save uses it.
