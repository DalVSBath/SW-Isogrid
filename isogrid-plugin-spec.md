# SolidWorks Isogrid Generator Plugin — Implementation Spec

> **Audience:** Claude Code (or another implementing engineer)
> **Goal:** Build a SolidWorks add-in (C# / .NET Framework) that auto-generates parametric isogrid patterns on user-selected surfaces.
> **Status:** Greenfield. No existing codebase.

---

## 1. What we're building

A SolidWorks add-in that lets an engineer select a face, enter isogrid parameters in a property manager page, and have the plugin automatically generate a triangular (equilateral) rib lattice cut into that face. The result should be a single collapsible feature in the feature tree that can be edited, suppressed, or re-generated.

### Why this matters (engineering context)

An **isogrid** is an integrally-stiffened lightweight structure — a thin skin reinforced with a lattice of equilateral triangular ribs. Originally patented by McDonnell Douglas in 1975, it's used heavily in launch vehicle interstages, payload shrouds, propellant tanks, and satellite primary structures. The equilateral triangle arrangement gives it quasi-isotropic in-plane stiffness — it behaves like an isotropic sheet at the panel level while using far less material.

Manually sketching hundreds of triangles on a curved skin is tedious and error-prone. This plugin automates that.

### Isogrid parameters (user inputs)

| Parameter | Symbol | Description |
|-----------|--------|-------------|
| Skin thickness | `t` | Residual skin thickness at the bottom of each triangular pocket |
| Rib thickness | `b` | Width of the ribs separating triangles |
| Rib depth | `d` | How deep the pocket is cut (depth of the rib) |
| Triangle edge length | `a` | Edge length of each equilateral triangle (derived height `h = a·√3/2`) |
| Corner fillet radius | `r` | Internal fillet at triangle corners (stress-concentration mitigation) |

---

## 2. Technology stack

- **Language:** C# targeting **.NET Framework 4.8** (SolidWorks add-ins are COM in-process servers; .NET Framework is the path of least resistance — avoid .NET 5/6/7/8 for V1)
- **IDE:** Visual Studio 2022 Community or higher
- **Wrapper framework:** **SolidDNA** (via NuGet: `CADBooster.SolidDna`) — wraps the raw COM API and handles registry/COM plumbing. Repo: https://github.com/CAD-Booster/SolidDNA
- **SolidWorks target version:** 2022 or newer (pick one, document it — API behavior differs subtly between versions)
- **Build output:** A COM-registered DLL installed into `C:\ProgramData\<YourCompany>\IsogridGenerator\` with a SolidWorks add-in registry entry under `HKLM\SOFTWARE\SolidWorks\Addins\{GUID}`

### Why SolidDNA over raw COM interop
Raw interop works but forces you to manage `ISwAddin`, `ICOMObject` lifetimes, registry keys, and PIA strong-naming yourself. SolidDNA wraps all of that and exposes an idiomatic C# API. If SolidDNA doesn't wrap a method you need, you can always drop down to the underlying `SolidWorks.Interop.sldworks` interfaces.

---

## 3. High-level architecture

```
IsogridGenerator (solution root)
├── IsogridGenerator.AddIn/          # Add-in entry point (ISwAddin)
│   ├── IsogridAddIn.cs              # SolidDNA SolidAddIn subclass
│   ├── CommandManager/              # Toolbar/menu registration
│   └── Properties/
├── IsogridGenerator.UI/             # Property manager page
│   ├── IsogridPropertyManagerPage.cs
│   └── IsogridInputs.cs             # POCO for user inputs
├── IsogridGenerator.Core/           # Pure geometry + algorithm (no SW refs)
│   ├── Grid/
│   │   ├── TriangleGridGenerator.cs # 2D equilateral grid math
│   │   └── GridPoint.cs
│   ├── Surfaces/
│   │   ├── ISurfaceStrategy.cs
│   │   ├── PlanarStrategy.cs
│   │   ├── CylindricalStrategy.cs
│   │   ├── ConicalStrategy.cs
│   │   └── FreeformProjectionStrategy.cs
│   └── Parameters/
│       └── IsogridParameters.cs
├── IsogridGenerator.SW/             # SolidWorks API interactions
│   ├── FaceAnalyzer.cs              # Identifies surface type from IFace2
│   ├── SketchBuilder.cs             # Creates sketches via ISketchManager
│   ├── FeatureBuilder.cs            # Creates cuts/wraps/split lines
│   └── MacroFeatureHost.cs          # Wraps output as a re-generable macro feature
└── IsogridGenerator.Tests/          # xUnit tests for Core (geometry-only)
```

**Key design principle:** keep `IsogridGenerator.Core` completely free of SolidWorks references. Everything in there is pure math and can be unit-tested without SolidWorks running. All SW API calls happen in `IsogridGenerator.SW` and `IsogridGenerator.UI`.

---

## 4. User workflow

1. User opens a part in SolidWorks and has the add-in loaded
2. User clicks the **Isogrid** button in the CommandManager
3. A property manager page opens on the right side with:
   - A face selection box (filter: `swSelFACES` only)
   - Numeric inputs for `a`, `b`, `d`, `t`, `r`
   - A live-preview toggle
   - OK / Cancel buttons
4. User selects a face, enters parameters, clicks OK
5. The plugin:
   - Analyzes the selected face's underlying surface type
   - Dispatches to the correct generation strategy
   - Creates the isogrid as a single macro feature in the tree
6. User can double-click the feature to edit parameters and regenerate

---

## 5. The geometry problem — three strategies

The core complexity is mapping a 2D equilateral triangle grid onto a 3D surface. The right approach depends on the surface type.

### 5.1 Planar surfaces (easy)

Plain-old sketch-and-cut. Create a sketch on the face, lay down a staggered grid of equilateral triangles, fillet the corners, cut-extrude to depth `d`.

**Triangle grid math:** for triangle edge `a`, rows are spaced vertically by `h = a·√3/2`. Odd rows are offset horizontally by `a/2` relative to even rows. Each vertex connects to 6 neighbors (to the left, right, upper-left, upper-right, lower-left, lower-right), giving the equilateral pattern.

```
Row 0:  •———•———•———•———•
         \ / \ / \ / \ /
Row 1:    •———•———•———•
         / \ / \ / \ / \
Row 2:  •———•———•———•———•
```

Trim any triangles that extend beyond the face boundary.

### 5.2 Cylindrical and conical surfaces (medium)

These are **developable** surfaces — they can be unrolled to a flat plane without distortion. The SolidWorks **Wrap feature** (`IFeatureManager.InsertWrapFeature`) is designed for exactly this: it maps a 2D sketch onto an analytical surface while preserving geodesic distances. Use "Deboss" mode with depth `d`.

- For a cylinder of radius `R`, the unrolled width is `2πR`. Generate a flat grid of that width × the cylinder height, then wrap.
- For a cone, unroll to the equivalent flat sector, generate grid within the sector, then wrap.

### 5.3 Freeform double-curvature surfaces (hard)

This is the genuinely difficult case — and the one where the plugin adds the most value. Three sub-strategies, pick based on V1 scope:

**Option A: Project-and-cut (simplest, geometrically imperfect)**
Build the flat grid in a sketch on a plane above the surface. Use `IFeatureManager.InsertProjectCurveFeature` to project onto the face. Cut-extrude with "offset from surface" end condition at depth `d`. **Limitation:** projection preserves XY position, not arc length, so triangles get stretched in high-curvature regions. Aesthetically fine, structurally wrong.

**Option B: Parameter-space meshing (correct but complex)**
Query the face's underlying `ISurface`, get its UV parameterization via `ISurface.Evaluate`. Generate the triangle grid in UV space, evaluate each vertex to get its 3D position. To keep triangles truly equilateral *on the surface* (not just in UV), compensate for the first fundamental form — walking geodesic distances rather than uniform UV steps. Then build a 3D sketch through the computed points.

**Option C: Voronoi/Delaunay on sampled points (most robust)**
Sample points on the surface using geodesic distance, then run a Delaunay triangulation constrained to that geodesic metric. Overkill for V1.

**V1 recommendation:** Ship Option A with a warning dialog ("Freeform surfaces will produce non-uniform triangles due to projection distortion"). Note Option B as a V2 roadmap item.

---

## 6. Detailed implementation steps

### Phase 1 — Project setup & "Hello World" add-in

1. Install SolidWorks 2022+ and Visual Studio 2022
2. Create a new Class Library (.NET Framework 4.8) project
3. Install NuGet package `CADBooster.SolidDna`
4. Create a class that inherits from `SolidAddIn`, override `ConnectToSolidWorks` and `DisconnectFromSolidWorks`
5. Register with a GUID in `[Guid("...")]` and `[ComVisible(true)]` attributes
6. Post-build step: `regasm /codebase IsogridGenerator.AddIn.dll`
7. Launch SolidWorks — verify the add-in appears in Tools → Add-Ins and loads without error
8. Have `ConnectToSolidWorks` show a simple message box to confirm wiring works

### Phase 2 — CommandManager button

1. In `ConnectToSolidWorks`, register a CommandManager tab with a single button labeled "Isogrid"
2. Wire the button click handler to (for now) open a message box showing the active model name
3. Add a 24×24 and 16×16 icon (PNG) to the project as embedded resources

### Phase 3 — Face selection + Property Manager Page

1. Create `IsogridPropertyManagerPage.cs` — SolidDNA wraps `IPropertyManagerPage2`
2. Add controls:
   - Selection box filtered to `swSelType_e.swSelFACES`
   - Number boxes for `a`, `b`, `d`, `t`, `r` (all in mm, converted to meters internally since SW API is SI)
   - OK / Cancel buttons
3. Validation: `b < a/2`, `t > 0`, `d > 0`, `r < b/2`, face must be selected
4. On OK: collect values into an `IsogridParameters` POCO and hand off to the generator

### Phase 4 — Core geometry (no SW refs)

1. Implement `TriangleGridGenerator` that, given a bounding box and parameter `a`, emits a list of line segments forming the equilateral grid
2. Implement the offset logic: each triangle is shrunk inward by `b/2` to create the rib walls (ribs sit between adjacent triangle boundaries)
3. Implement corner fillet generation — at each vertex, replace the sharp corner with an arc of radius `r`
4. Write xUnit tests verifying:
   - A 100×100 region with `a=10` produces the expected triangle count
   - All triangle edges are length `a ± ε`
   - All internal angles are 60° ± ε

### Phase 5 — Surface analysis & strategy dispatch

1. Implement `FaceAnalyzer.Identify(IFace2 face)` that returns an enum: `Planar`, `Cylindrical`, `Conical`, `Spherical`, `Freeform`
2. Query via `face.GetSurface()` then `ISurface.Identity` (returns `swSurfaceTypes_e`)
3. Implement strategy pattern — each surface type maps to an `ISurfaceStrategy` implementation

### Phase 6 — Planar strategy (simplest path to a working demo)

1. Create a sketch on the selected face via `IModelDoc2.SketchManager.InsertSketch(true)`
2. Get the face's bounding box to size the grid
3. Emit all grid line segments via `ISketchManager.CreateLine`
4. Add fillets at vertices via `ISketchManager.CreateFillet`
5. Trim segments that fall outside the face boundary
6. Exit sketch, then call `IFeatureManager.FeatureCut4` with depth `d`
7. **Wrap the whole sequence in `IModelDoc2.StartUndoStep` / `EndUndoStep`** so it's a single undo action
8. **Disable graphics updates during generation** via `IModelView.EnableGraphicsUpdate = false` — can 10×+ the speed for large grids

### Phase 7 — Cylindrical strategy (using Wrap)

1. Create a tangent plane to the cylinder at a reference point
2. On that plane, sketch the flat grid sized to `2πR × H`
3. Apply fillets
4. Use `IFeatureManager.InsertWrapFeature` with mode = Deboss, depth = `d`, target = selected face

### Phase 8 — Freeform strategy (project-and-cut)

1. Create a reference plane offset above the face
2. Sketch the flat grid on that plane
3. Use `IFeatureManager.InsertProjectCurveFeature` with "Sketch on faces" option
4. Use the projected curves as the profile for a cut-extrude with "Offset from surface" end condition, depth `d`
5. Show a warning dialog before generation: "Pattern will have non-uniform triangle sizes on double-curvature surfaces"

### Phase 9 — Macro feature wrapping (the "right way" to ship)

1. Instead of leaving 400 individual features in the tree, wrap the output in a single macro feature via `IFeatureManager.InsertMacroFeature3`
2. Store the `IsogridParameters` in the macro feature's data via `IMacroFeatureData.SetParameters`
3. Implement `ISwComFeature` with `Regenerate`, `Edit`, `Security` callbacks
4. Now the feature shows as a single "Isogrid1" entry in the tree, editable via double-click

### Phase 10 — Performance hardening

For large grids (think thousands of triangles), these settings can make the difference between "30 seconds" and "unusable":

- `IModelView.EnableGraphicsUpdate = false` during generation
- `IFeatureManager.EnableFeatureTree = false`
- `IFeatureManager.EnableFeatureTreeWindow = false`
- `IModelDocExtension.SetUserPreferenceToggle(swUserPreferenceToggle_e.swSolveOnLoad, swUserPreferenceOption_e.swDetailingNoOptionSpecified, false)`
- Batch sketch entity creation — create all lines first, then all fillets, rather than interleaving

Always re-enable these in a `finally` block so a crash doesn't leave SolidWorks in a weird state.

---

## 7. Key SolidWorks API references

| Task | Interface / Method |
|------|-------------------|
| Top-level app | `ISldWorks` |
| Active document | `IModelDoc2` |
| Feature creation | `IFeatureManager` |
| Sketch creation | `ISketchManager` |
| Selection | `ISelectionMgr`, `IModelDocExtension.SelectByID2` |
| Face geometry | `IFace2.GetSurface()` → `ISurface` |
| Surface type | `ISurface.Identity` returns `swSurfaceTypes_e` |
| UV evaluation | `ISurface.Evaluate(u, v, numDeriv)` |
| Sketch line | `ISketchManager.CreateLine(x1,y1,z1,x2,y2,z2)` |
| Sketch fillet | `ISketchManager.CreateFillet(radius, opts)` |
| Cut extrude | `IFeatureManager.FeatureCut4(...)` |
| Wrap feature | `IFeatureManager.InsertWrapFeature(...)` |
| Project curve | `IFeatureManager.InsertProjectCurveFeature(...)` |
| Split line | `IFeatureManager.InsertSplitLineFeature(...)` |
| Macro feature | `IFeatureManager.InsertMacroFeature3(...)` |
| Undo grouping | `IModelDoc2.StartUndoStep / EndUndoStep` |

**Official docs:** https://help.solidworks.com/2024/english/api/sldworksapiprogguide/Welcome.htm

**Excellent third-party resources:**
- CodeStack: https://www.codestack.net/solidworks-api/
- CADBooster blog (SolidDNA authors): https://cadbooster.com/
- Hawk Ridge Systems API blog series

---

## 8. Gotchas to warn Claude Code about

1. **All SW API units are SI.** Millimeter input from the UI must be multiplied by 0.001 before passing to the API. This is the #1 source of "why is my part 1000× too big" bugs.
2. **COM reference release.** SolidDNA handles most of this, but if you drop to raw interop, wrap objects in `using` blocks or explicitly call `Marshal.ReleaseComObject`. Leaked references cause SolidWorks to hang on close.
3. **Methods come in versioned flavors.** `FeatureExtrusion`, `FeatureExtrusion2`, `FeatureExtrusion3` — always use the highest-numbered version available in your target SW version.
4. **The API does not throw exceptions for most failures.** Methods return `null` or a boolean. Check every return value.
5. **Registry on 64-bit systems.** Add-ins must be registered under `HKLM\SOFTWARE\SolidWorks\Addins\`, not `Wow6432Node`. Use 64-bit `regasm` (from `Framework64`, not `Framework`).
6. **"Edit feature" on a macro feature re-opens the Property Manager Page.** Your `Edit` callback in `ISwComFeature` is what makes this work — don't skip it.
7. **Selection state is fragile.** Clear selections with `IModelDoc2.ClearSelection2(true)` before programmatically selecting entities for a feature operation.
8. **`FeatureCut4`'s parameter list is monstrous** — roughly 25 parameters. Read the docs carefully and consider writing a thin wrapper method that takes a config object.

---

## 9. Scope for V1 vs. later

**V1 (ship this first):**
- Planar faces ✅
- Cylindrical faces via Wrap ✅
- Conical faces via Wrap ✅
- Freeform via project-and-cut with warning ✅
- Single-face selection ✅
- Macro feature output ✅

**V2 / later:**
- True geodesic UV meshing for freeform surfaces
- Multi-face selection (e.g., an entire tank dome)
- Automatic node pads (circular bosses at triangle vertices, common in real isogrids)
- Variable triangle size (grading — smaller triangles in high-stress regions)
- Export to FEA-ready mesh directly
- Composite/3D-print-optimized rib cross-sections (T-section, I-section instead of rectangular)

---

## 10. Testing strategy

- **Unit tests** (xUnit, on `IsogridGenerator.Core`): geometry math, triangle count, edge lengths, angles
- **Integration tests:** a small harness that launches SolidWorks, opens a test part with a known flat face, runs the generator, and verifies the resulting feature count and face count
- **Manual test matrix:** one part per surface type (plane, cylinder, cone, sphere, loft), one screenshot per result, for visual regression

---

## 11. Deliverables checklist

- [ ] Working add-in DLL that loads in SolidWorks 2022+
- [ ] Toolbar button in CommandManager
- [ ] Property Manager Page with face selection and parameter inputs
- [ ] Planar strategy implemented and tested
- [ ] Cylindrical strategy implemented and tested
- [ ] Conical strategy implemented and tested
- [ ] Freeform (projection) strategy implemented with warning
- [ ] Output as a single re-editable macro feature
- [ ] Input validation + user-facing error messages
- [ ] README with install instructions and screenshots
- [ ] Unit tests for Core (target 80%+ coverage)
- [ ] Sample parts (one per surface type) demonstrating output

---

## 12. Getting started for Claude Code

```bash
# After cloning or creating the repo:
# 1. Open solution in Visual Studio 2022
# 2. Restore NuGet packages (SolidDNA will pull in SolidWorks interops)
# 3. Set post-build event on IsogridGenerator.AddIn:
#    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" /codebase "$(TargetPath)"
# 4. Set Debug → Start External Program to SLDWORKS.exe
# 5. F5 to launch SolidWorks with the add-in attached for debugging
```

First milestone: get Phase 1 + Phase 2 working — a button that does nothing but show a message box. Everything else builds from there.

Good luck. The hardest part isn't the SolidWorks API or the geometry — it's the hundred small COM and registry gotchas. Move slowly, commit often, and keep `Core` free of SW references so you can sanity-check the math in isolation.
