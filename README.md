# Workbench Ingredients

A RimWorld 1.5/1.6 mod that moves crafting ingredient-sourcing from **per-bill** to **per-workbench**. (packageId `archdukejim.WorkbenchIngredients`)

Each work table gets an **Ingredient sources** button in its Bills tab. Open it and check any combination of:

- stockpile **zones**
- storage **buildings** (shelves/racks)
- storage **groups**

The bench draws ingredients from the **union** of everything checked, **map-wide**, and ignores every bill's own ingredient search radius.

If nothing is checked, the bench falls back to a single **per-bench radius** (pre-filled from a global default in mod settings), applied to all bills at that bench — overriding each bill's own radius.

Only *where* ingredients are searched changes. The ingredient filter, required counts, allow-mixing, reachability and forbidden checks are all vanilla: the mod reuses the game's own ingredient matcher and only swaps the candidate set.

## How it works

| Piece | File |
|---|---|
| Comp added to every work table (stores the per-bench selection + fallback radius) | `Source/CompWorkbenchIngredients.cs` |
| XML that attaches the comp to `Building_WorkTable` defs | `Patches/AddWorkbenchIngredientsComp.xml` |
| Runtime net that also attaches it to `Building_WorkTable` *subclasses* | `WorkbenchCompInjector` in `Source/WorkbenchIngredientsMod.cs` |
| Ingredient-search override (both selected-sources and fallback-radius paths) | `Source/Patch_WorkGiver_DoBill.cs` |
| "Ingredient sources" tab button | `Source/Patch_ITab_Bills.cs` |
| Sources window (checklist + fallback slider) | `Source/Window_WorkbenchIngredients.cs` |
| Global default-radius setting | `WorkbenchIngredientsSettings` in `Source/WorkbenchIngredientsMod.cs` |

The override patches the private `WorkGiver_DoBill.TryFindBestBillIngredients` and, for selected sources, calls the private set-matcher `WorkGiver_DoBill.TryFindBestBillIngredientsInSet` by reflection (resolved by name, arguments bound by type) so it degrades gracefully if a future version renames it. Both signatures were verified against 1.6 `Assembly-CSharp.dll`.

## Building

```bash
dotnet build Source/WorkbenchIngredients.csproj -c Release
```

Output goes to `Assemblies/WorkbenchIngredients.dll`. Adjust `RimWorldPath` / the Harmony `HintPath` in the `.csproj` if your install differs.

## Keep it loaded

The mod attaches a component to every work-table def to store per-bench selections. Removing the mod is safe (benches revert to vanilla per-bill radius) but discards every bench's configuration and logs one-time "could not load" warnings, so keep it enabled while a save uses it.
