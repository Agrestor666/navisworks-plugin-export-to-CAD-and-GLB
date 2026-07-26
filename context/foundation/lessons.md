# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Resolve plugin dependencies from the plugin folder on Manage 2026

- **Context**: Any `*.2026` Add-In plugin that references private assemblies (Geometry twin, NuGet writers such as SharpGLTF or ACadSharp) under `{NavisworksInstallDir2026}Plugins\<AssemblyName>\`.
- **Problem**: Navisworks loads the plugin DLL without probing that folder. JIT-compiling a method that references a missing dependency throws `FileNotFoundException` and can hard-crash the host before any user-facing error handler runs (S-03 GLB 2026).
- **Rule**: Install `AppDomain.CurrentDomain.AssemblyResolve` at the very start of `Execute`, before any method that references private assemblies is JIT-compiled. Copy the `PluginAssemblyResolver` pattern from `NavisworksExport.Glb.2026/GlbExportCommand.cs`. Deploy every private `*.dll` (and `*.pdb` for diagnostics) into the plugin folder — not only the plugin assembly.
- **Applies to**: plan, implement, impl-review

## Keep export logic out of Execute with NoInlining

- **Context**: Manage 2026 export command entry points (`AddInPlugin.Execute`).
- **Problem**: Type-load and assembly-resolve failures happen when the CLR JIT-compiles the first method that touches missing types. If that method is `Execute` itself and the handler is too late, the host crashes unhandled.
- **Rule**: `Execute` should only install the assembly resolver and call a separate `RunExport` marked `[MethodImpl(MethodImplOptions.NoInlining)]`, wrapped in try/catch that logs to `%TEMP%` and shows a message box. Mirror `NavisworksExport.Glb.2026/GlbExportCommand.cs`.
- **Applies to**: plan, implement, impl-review

## COM geometry callbacks must be public and ComVisible

- **Context**: `InwSimplePrimitivesCB` implementations used with `GenerateSimplePrimitives` in `NavisworksExport.Geometry`.
- **Problem**: An `internal` callback class cannot be marshalled as a COM callable wrapper; Navisworks crashes or silently fails to deliver triangles (S-03 GLB 2026).
- **Rule**: Implement `PrimitiveCallback` (or equivalent) as a `public` class with `[ComVisible(true)]`. Initialize `ComApiBridge.State` before crossing the COM boundary. Reference `Autodesk.Navisworks.ComApi` explicitly in 2026 plugin projects that use geometry extraction.
- **Applies to**: research, plan, implement, impl-review

## Navisworks COM matrices are column-major

- **Context**: Transforming local fragment coordinates to world space via `InwOaFragment3.GetLocalToWorldMatrix()` / `InwLTransform3f.Matrix`.
- **Problem**: Treating the 16-element matrix as row-major puts translation at the wrong indices and collapses all geometry to one point (S-03 GLB 2026).
- **Rule**: Read translation from indices 12/13/14 and multiply points as `m[0]*x + m[4]*y + m[8]*z + m[12]` (and the analogous rows for Y/Z). Apply the 3×3 rotation only (no translation) when transforming normals.
- **Applies to**: implement, impl-review

## Guard frag.Geometry on Manage 2026

- **Context**: Fragment dedup in `SelectionGeometryExtractor` that keys on `InwOaFragment3.Geometry`.
- **Problem**: Manage 2026 raises `COMException` «Not implemented» on `get_Geometry()`; an unguarded access crashes export mid-save (S-03 GLB 2026).
- **Rule**: Wrap `frag.Geometry` in try/catch; on failure, disable geometry-reference dedup for the rest of the export and continue extracting per fragment. Do not assume dedup works identically across hosts.
- **Applies to**: implement, impl-review

## Expand selection to geometry-bearing leaves before COM conversion

- **Context**: Any export that passes `ModelItemCollection` to `ComApiBridge.ToInwOpSelection` (`Extract`, `ExtractGrouped`, future extractors).
- **Problem**: Selecting a composite/group node yields COM paths whose fragments belong to child items. The path-identity filter then skips every fragment, so export looks random — whole branches export as nothing while individually clicked leaves work (S-03 GLB 2026).
- **Rule**: Before COM conversion, expand the selection with `DescendantsAndSelf` to all items where `HasGeometry` is true (dedupe with a `HashSet`). Fall back to the raw selection only when no geometry leaves are found. Reuse the same expansion in every public extract entry point.
- **Applies to**: plan, implement, impl-review

## Do not trust InwSimpleVertex.color from GenerateSimplePrimitives

- **Context**: COM triangle extraction in `PrimitiveCallback` / `SelectionGeometryExtractor`.
- **Problem**: Requesting `eCOLOR` still returns zeroed vertex colors on Manage 2026; exports appear black or wrong unless color is sourced elsewhere (S-03 GLB 2026).
- **Rule**: Prefer `ModelItem.Geometry.ActiveColor` and `ActiveTransparency` via `ComApiBridge.ToModelItem(path)`; fall back to fragment appearance material, then a neutral default. Do not rely on per-vertex COM color arrays for MVP fidelity.
- **Applies to**: implement, impl-review

## GLB PBR defaults render black without an environment map

- **Context**: glTF material setup in `GlbWriter` only (PowerPoint / glTF viewers).
- **Problem**: SharpGLTF's default `metallicFactor` is 1.0; without an environment map every surface renders black even when vertex colors are correct (S-03 GLB 2026).
- **Rule**: For vertex-color GLB exports, set `metallicFactor = 0` and a sensible `roughnessFactor` (e.g. 0.8). Do not apply this to DWG/AutoCAD writers — AutoCAD is Z-up and uses different material semantics.
- **Applies to**: implement, impl-review
