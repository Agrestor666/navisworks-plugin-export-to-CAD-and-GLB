# Export Selection to GLB on Navisworks Manage 2026 Implementation Plan

## Overview

Port the proven S-01 "Export to GLB" flow onto the Manage **2026** twin host (`NavisworksExport.Glb.2026`, scaffolded by F-02): replace its selection-count stub with the real COM-extraction → glTF-writing → save-dialog flow, sharing source with the 2023 implementation via linked files instead of duplicating the COM/dedup/axis-conversion logic.

## Current State Analysis

`NavisworksExport.Glb` (2023) is the fully working S-01 implementation: `NavisworksExport.Geometry` (COM selection → world-space colored triangles with fragment dedup) → `GlbWriter` (SharpGLTF, Z-up→Y-up + unit conversion, single colored mesh) → `GlbExportCommand` (empty-selection/zero-triangle errors, `SaveFileDialog`, success/error messages). `NavisworksExport.Glb.2026` (2026, from F-02) is only a stub: it references `Autodesk.Navisworks.Api` via `NavisworksInstallDir2026` and shows a selection-count `MessageBox`. It has no reference to `NavisworksExport.Geometry`, no `SharpGLTF.Toolkit` package, and its `.csproj` doesn't reference the COM assemblies (`Autodesk.Navisworks.ComApi`, `Autodesk.Navisworks.Interop.ComApi`) that geometry extraction needs. `NavisworksExport.Geometry` today is single-target: it resolves its three Navisworks assemblies exclusively via `$(NavisworksInstallDir)` (2023), with an `NavisworksAPIdlls2023` NuGet fallback for off-host compiles.

## Desired End State

Clicking "Export to GLB" in Navisworks Manage **2026** with a non-empty, geometry-bearing selection opens a save dialog, writes a `.glb` file, and that file opens in PowerPoint (Insert 3D Model) as a correctly oriented, colored, interactive 3D model — identical user-facing behavior to the 2023 plugin. Empty selections and selections with no extractable geometry show the same clear-error pattern as 2023. The source Navisworks document is never modified. The 2023 plugin, host, and behavior are completely unaffected.

**Verification**: build the solution (Manage 2026 installed, elevated terminal), load `NavisworksExport.Glb.2026` in Navisworks Manage 2026, run it against a real selection, and open the resulting `.glb` in PowerPoint to confirm it renders and rotates correctly — repeating S-01's full manual test matrix on the new host.

### Key Discoveries:

- **Empirically confirmed, not assumed**: both Manage 2023 and Manage 2026 are installed on this machine. Test-compiling `NavisworksExport.Geometry` and the full `NavisworksExport.Glb` project graph (COM interop, `GenerateSimplePrimitives`, `ComApiBridge.ToInwOpSelection`, `InwSimplePrimitivesCB`, `UnitConversion.ScaleFactor`, `Application.ActiveDocument`) against Manage 2026's `Autodesk.Navisworks.Api.dll` / `Autodesk.Navisworks.ComApi.dll` / `Autodesk.Navisworks.Interop.ComApi.dll` (by overriding `NavisworksInstallDir` to the 2026 path) **succeeds with zero errors and zero source changes**. This resolves the roadmap's open unknown ("what breaking changes exist between 2023 and 2026 COM/geometry API") — none were found at the source-API-surface level this feature uses.
- Because the same source compiles against either host, the plan shares source via **linked `.cs` files** across twin projects rather than duplicating ~300 lines of COM/dedup logic (`SelectionGeometryExtractor.cs`, `PrimitiveCallback.cs`, `ExtractedTriangle.cs`) or the axis/unit-conversion logic in `GlbWriter.cs` — both are bug-prone, community-sourced pieces per S-01's own risk notes, and duplicating them risks silent drift between hosts.
- `NavisworksExport.Glb.2026`'s current deploy target (`NavisworksExport.Glb.2026/NavisworksExport.Glb.2026.csproj:25-28`) only copies `$(TargetPath)` — the single plugin DLL. Once this plugin gets a `ProjectReference` (`Geometry.2026`) and a `PackageReference` (`SharpGLTF.Toolkit`), its dependent DLLs also need deploying, mirroring the wildcard-with-exclusion pattern already used by 2023's `NavisworksExport.Glb.csproj:40-47`.
- F-02 deliberately gave the 2026 stub a distinct `Plugin` id (`"GlbExport2026"`) while keeping the same `DisplayName`/`ToolTip` as 2023 (`NavisworksExport.Glb.2026/GlbExportCommand.cs:7`) — the real command keeps that same id, and per the confirmed UX decision, all other user-facing text matches 2023 exactly.
- `roadmap.md` is untracked in git and not touched by the `/10x-implement` epilogue convention (confirmed: the S-01 and F-02 "close out plan" commits only touch `change.md`/`plan.md`) — so it currently shows F-02 as `ready` even though F-02 is fully implemented. Nothing else updates it automatically; this plan's final phase fixes it directly.

## What We're NOT Doing

- Anything on `NavisworksExport.AutoCad.2026` (S-04) — out of scope, still blocked on S-02 per the roadmap.
- Retargeting, multi-targeting, or otherwise modifying the 2023 `NavisworksExport.Glb` / `NavisworksExport.Geometry` projects — they stay exactly as S-01 left them; 2026 only adds new linked-file twins.
- Any 2026-specific UX difference (host-hint text, different dialog copy) — confirmed decision is identical user-facing behavior between hosts.
- Re-running the `tools/GlbWriterHarness` hardcoded-triangle sanity check against the `.2026` build — confirmed decision is to rely on identical linked source (already sanity-checked in S-01) and go straight to live host verification.
- A `NavisworksAPIdlls2026` NuGet fallback for `NavisworksExport.Geometry.2026` — matches F-02's decision that 2026 projects are install-required only.
- Any change to `NavisworksExport.AutoCad`, `NavisworksExport.AutoCad.2026`, PRD, or AGENTS.md — only `roadmap.md`'s stale status is in scope for docs.

## Implementation Approach

Add one new twin project (`NavisworksExport.Geometry.2026`) whose `.csproj` links back to the existing `NavisworksExport.Geometry/*.cs` files and references the 2026 host's COM assemblies, then wire `NavisworksExport.Glb.2026` to consume it exactly the way 2023's `NavisworksExport.Glb` consumes the original `NavisworksExport.Geometry` — including linking `GlbWriter.cs` in rather than re-implementing it. Only `GlbExportCommand.cs` in the 2026 project gets real (non-linked) content, since it already carries a host-distinct `Plugin` id. Finish with a full live-host verification pass and a roadmap status fix.

## Phase 1: Shared geometry-extraction twin

### Overview

Add `NavisworksExport.Geometry.2026`, a class library that compiles the exact same COM-extraction source as `NavisworksExport.Geometry` against Manage 2026's host assemblies.

### Changes Required:

#### 1. New twin project

**File**: `NavisworksExport.Geometry.2026/NavisworksExport.Geometry.2026.csproj`

**Intent**: A `net48`/x64 SDK-style class library that produces a `NavisworksExport.Geometry.2026`-named assembly from the same four source files as `NavisworksExport.Geometry` (`ExtractedTriangle.cs`, `PrimitiveCallback.cs`, `SelectionGeometryExtractor.cs`), linked rather than copied, referencing Manage 2026's `Autodesk.Navisworks.Api`, `Autodesk.Navisworks.ComApi`, and `Autodesk.Navisworks.Interop.ComApi` via `HintPath="$(NavisworksInstallDir2026)..."`.

**Contract**: `TargetFramework=net48`, `PlatformTarget=x64`, `Nullable=enable`, `AssemblyName`/`RootNamespace` = `NavisworksExport.Geometry.2026`. Compile items are `<Compile Include="..\NavisworksExport.Geometry\ExtractedTriangle.cs" Link="ExtractedTriangle.cs" />` (and the same for the other two files) — no copies. HintPath-only references to the three 2026 assemblies (`Private=False`, `SpecificVersion=False`, matching `NavisworksExport.Glb.2026.csproj`'s existing 2026-reference style), unconditional (no `NavisworksAPIdlls*` fallback, per F-02's install-required convention). No `AddInPlugin`, no deploy target — this is a plain library, same as `NavisworksExport.Geometry` today.

#### 2. Solution membership

**File**: `NavisworksExportPlugins.sln`

**Intent**: One `dotnet build` entry point covers the new project.

**Contract**: Add the project with the same x64-only `ActiveCfg`/`Build.0` mappings used by every existing project (`Debug|Any CPU` → `Debug|x64`, `Debug|x86` → `Debug|x64`, same for `Release`). Prefer `dotnet sln add` then verify the `GlobalSection(ProjectConfigurationPlatforms)` entries match the pattern at `NavisworksExportPlugins.sln:50-61`.

### Success Criteria:

#### Automated Verification:

- With Manage 2026 installed (or `NavisworksInstallDir2026` set), `dotnet build NavisworksExportPlugins.sln` succeeds and produces a `net48`/x64 `NavisworksExport.Geometry.2026.dll`
- `NavisworksExport.Geometry.2026.csproj` contains no `NavisworksAPIdlls*` / third-party NuGet API `PackageReference`
- Existing 2023 projects and `NavisworksExport.Geometry` (2023) still build unchanged in the same solution

#### Manual Verification:

- No standalone manual verification for this phase — the new library has no UI hook yet; it is exercised together with Phase 4's host verification

---

## Phase 2: GLB writer wiring on the 2026 twin

### Overview

Give `NavisworksExport.Glb.2026` the same writer capability as 2023, by linking in `GlbWriter.cs` and wiring its two dependencies (SharpGLTF, Geometry.2026), and fix its deploy target to carry those dependencies into the Manage 2026 Plugins folder.

### Changes Required:

#### 1. Linked GLB writer

**File**: `NavisworksExport.Glb.2026/GlbWriter.cs` (linked)

**Intent**: Reuse the exact Navisworks→glTF axis/unit conversion and SharpGLTF mesh-building logic from 2023 without a second copy to maintain.

**Contract**: `<Compile Include="..\NavisworksExport.Glb\GlbWriter.cs" Link="GlbWriter.cs" />` in `NavisworksExport.Glb.2026.csproj` — no new file on disk, no source changes. The linked file's `namespace NavisworksExport.Glb` stays as-is; it compiles fine into the `NavisworksExport.Glb.2026` assembly regardless of that assembly's `RootNamespace`.

#### 2. Package and project references

**File**: `NavisworksExport.Glb.2026/NavisworksExport.Glb.2026.csproj`

**Intent**: Give the twin the same `SharpGLTF.Toolkit` package and a reference to the 2026 geometry twin (not the 2023 one).

**Contract**: `<PackageReference Include="SharpGLTF.Toolkit" Version="1.0.6" />` (same version as 2023) and `<ProjectReference Include="..\NavisworksExport.Geometry.2026\NavisworksExport.Geometry.2026.csproj" />`.

#### 3. Deploy target fix

**File**: `NavisworksExport.Glb.2026/NavisworksExport.Glb.2026.csproj`

**Intent**: The current deploy target only copies the single plugin DLL; once this project has a project reference and a NuGet dependency, those dependent DLLs (`NavisworksExport.Geometry.2026.dll`, `SharpGLTF.*.dll`, transitive deps) must land in the same Plugins folder or the plugin will fail to load at runtime.

**Contract**: Replace the single-file `Copy` with the wildcard-with-exclusion pattern already proven in 2023's `NavisworksExport.Glb.csproj:40-47` (`@(_DeployDlls)` = `$(TargetDir)*.dll` excluding `$(TargetDir)Autodesk.*.dll`), keeping `OverwriteReadOnlyFiles="true"` and `ContinueOnError="WarnAndContinue"` on both `MakeDir` and `Copy` (no `RemoveDir`, per the F-01/F-02 lesson already encoded in both existing deploy targets).

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with `SharpGLTF.Toolkit` restored and the `Geometry.2026` `ProjectReference` resolved for `NavisworksExport.Glb.2026`
- 2023's `NavisworksExport.Glb` still builds unaffected (no shared package/project state broken)

#### Manual Verification:

- No standalone manual verification for this phase — deploy-target correctness (all dependent DLLs actually copied) is confirmed as part of Phase 4's host verification

---

## Phase 3: Command wiring

### Overview

Replace the `GlbExportCommand.cs` stub in `NavisworksExport.Glb.2026` with the real export flow, matching 2023's behavior and text exactly.

### Changes Required:

#### 1. Command implementation

**File**: `NavisworksExport.Glb.2026/GlbExportCommand.cs`

**Intent**: Replace the selection-count `MessageBox` stub with the same flow as `NavisworksExport.Glb/GlbExportCommand.cs`: validate non-empty selection (error + return on empty, no dialog), show a `.glb`-filtered `SaveFileDialog`, run `SelectionGeometryExtractor.Extract` (from `NavisworksExport.Geometry.2026`'s linked type), treat a zero-triangle result from a non-empty selection the same as the empty-selection error, otherwise compute the unit scale via `UnitConversion.ScaleFactor` and call the linked `GlbWriter.WriteGlb`, wrapped in a `try/catch` reporting failures as a message box.

**Contract**: Keep the file's existing `[Plugin("GlbExport2026", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]` / `[AddInPlugin(AddInLocation.AddIn)]` attributes and `Execute(params string[] parameters) : int` signature unchanged (only the `Plugin` id differs from 2023's `"GlbExport"`, per the confirmed decision). All dialog/error/success message text matches 2023's `Caption = "Export to GLB"` and message strings verbatim. `using NavisworksExport.Geometry;` resolves to the `Geometry.2026`-compiled types (same namespace, different assembly, via the `ProjectReference` from Phase 2); `GlbWriter` resolves via the linked file from Phase 2. Only ever reads from `Application.ActiveDocument` — no document-mutating calls, preserving the read-only guardrail.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds

#### Manual Verification:

- Empty selection → error message box, no save dialog shown, no file written
- Non-empty, geometry-bearing selection → save dialog appears, `.glb` written to the chosen path, success message shown
- Selection with zero extractable triangles → same error pattern as the empty-selection case, no file written
- Canceling the save dialog aborts silently — no error, no file written
- Source Navisworks document is unchanged after export

---

## Phase 4: Host verification & roadmap fix

### Overview

Confirm the full flow works live in Navisworks Manage **2026** against a real model and PowerPoint — repeating S-01's complete manual test matrix rather than a lighter regression pass, since this is the first live run of this compiled artifact on the new host — and correct `roadmap.md`'s stale F-02 status while touching the file for S-03.

### Changes Required:

#### 1. Manual host + PowerPoint verification

**File**: n/a (verification-only)

**Intent**: Build and deploy (elevated terminal/IDE) both the 2026 GLB chain, load Navisworks Manage 2026, and run the same test matrix S-01 ran on 2023: empty selection, real geometry-bearing selection, zero-triangle selection, a large/instanced-geometry selection (dedup performance), and PowerPoint rendering — because identical source compiling cleanly doesn't guarantee identical COM-marshaling or performance behavior on the new host at runtime.

**Contract**: n/a.

#### 2. Roadmap status fix

**File**: `context/foundation/roadmap.md`

**Intent**: F-02 is fully implemented (confirmed via `git log`: `2792c46`, `bfc5645`, `375901e`, `43b7d9a`) but the roadmap still shows it as `ready`/`proposed` in the "At a glance" table and the F-02 section — this plan is the natural point to fix that stale state, and to record S-03's own completion once Phase 4's manual verification passes.

**Contract**: Update the "At a glance" table's F-02 row `Status` from `ready` to `done`; update the `### F-02` section's `**Status:**` line to `done`; add an F-02 entry under `## Done` (mirroring the existing F-01 entry's format). Update S-03's row/section/`## Done` entry the same way once Phase 4's manual checks pass (mirroring S-01's existing `## Done` entry format). Do not alter F-01, S-01, S-02, S-04, or any other roadmap content.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds (full-solution final check)
- `roadmap.md`'s F-02 row/section no longer say `ready`; both F-02 and S-03 have `## Done` entries

#### Manual Verification:

- "Export to GLB" appears under Navisworks Manage 2026's Add-Ins tab and runs against a real selection from a real model
- Empty selection on the 2026 host → error message, no dialog, no file (same as Phase 3's build-time check, now confirmed live)
- Resulting `.glb` inserts into PowerPoint via Insert 3D Model and renders as an interactive, correctly oriented (upright), colored model that can be rotated/zoomed
- A reasonably large selection (dozens–hundreds of items with repeated/instanced geometry) extracts and exports without hanging, confirming the dedup strategy works identically on Manage 2026
- A selection with zero extractable triangles shows the same error pattern as empty selection, live on 2026
- Original Navisworks Manage 2026 document is unchanged after the full run
- Manage 2023's `NavisworksExport.Glb` plugin still loads and works unaffected (regression sanity, if 2023 is also installed/available)

---

## Testing Strategy

### Unit Tests:

- None planned — no test project exists in the repo yet (`AGENTS.md`). Same as S-01: the axis/unit conversion is a pure function and would be a good first candidate if a test project is introduced later, and it's the same shared source S-01 already covers.

### Integration Tests:

- None (manual verification only, per F-01/S-01/F-02's established approach).

### Manual Testing Steps:

1. Build the solution in an elevated terminal/IDE with Manage 2026 installed, so the post-build deploy step copies `NavisworksExport.Glb.2026.dll`, `NavisworksExport.Geometry.2026.dll`, and the SharpGLTF dependency DLLs into the Manage 2026 Plugins folder.
2. Open Navisworks Manage 2026 with a real model, select nothing, run "Export to GLB" → confirm error message, no dialog.
3. Select a group of objects with real mesh geometry, run "Export to GLB" → confirm save dialog, pick a location, confirm success message and that the `.glb` file exists on disk.
4. Open PowerPoint, Insert → 3D Models → This Device → select the `.glb` → confirm it renders upright, colored, and is interactively rotatable/zoomable.
5. Select a group with instanced/repeated geometry large enough to notice a performance difference, run the export, and confirm it completes without a long hang.
6. Select an item with no mesh geometry (collapsed grouping node or annotation-only item), run "Export to GLB" → confirm the zero-triangle error path, not a near-empty file.
7. Re-open or inspect the Manage 2026 document and confirm nothing about the source document changed.
8. If Manage 2023 is also installed, smoke-check that `NavisworksExport.Glb` (2023) still loads and runs correctly (regression sanity for the shared-source approach).

## Performance Considerations

No new performance work — this plan reuses S-01's fragment-dedup mitigation verbatim (via linked source). Phase 4's large/instanced-selection check exists specifically to confirm that mitigation behaves the same on Manage 2026's COM implementation, not to introduce new optimization.

## Migration Notes

Additive only. No changes to 2023 assemblies, no data migration. `NavisworksExport.Geometry.2026` and the widened `NavisworksExport.Glb.2026` are new/changed build outputs installed side-by-side with the unchanged 2023 plugin via distinct assembly/folder names.

## References

- Related plan: `context/changes/export-selection-glb/plan.md` (S-01 — the flow being ported)
- Related plan: `context/changes/nw-plugin-scaffold-2026/plan.md` (F-02 — the twin-project host pattern being extended)
- Existing reference pattern: `NavisworksExport.Glb/NavisworksExport.Glb.csproj:40-47` (deploy-target wildcard-with-exclusion pattern to mirror)
- Existing stub to replace: `NavisworksExport.Glb.2026/GlbExportCommand.cs`
- Roadmap: `context/foundation/roadmap.md` (S-03, F-02 status fix)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Shared geometry-extraction twin

#### Automated

- [x] 1.1 `dotnet build NavisworksExportPlugins.sln` succeeds and produces `NavisworksExport.Geometry.2026.dll` — c2464c9
- [x] 1.2 `NavisworksExport.Geometry.2026.csproj` contains no `NavisworksAPIdlls*` / third-party NuGet API reference — c2464c9
- [x] 1.3 Existing 2023 projects and `NavisworksExport.Geometry` still build unchanged — c2464c9

#### Manual

- [x] 1.4 No standalone check — exercised together with Phase 4 — c2464c9

### Phase 2: GLB writer wiring on the 2026 twin

#### Automated

- [x] 2.1 `dotnet build NavisworksExportPlugins.sln` succeeds with `SharpGLTF.Toolkit` restored and the `Geometry.2026` project reference resolved — d5dee34
- [x] 2.2 2023's `NavisworksExport.Glb` still builds unaffected — d5dee34

#### Manual

- [x] 2.3 No standalone check — deploy-target correctness confirmed in Phase 4 — d5dee34

### Phase 3: Command wiring

#### Automated

- [x] 3.1 `dotnet build NavisworksExportPlugins.sln` succeeds

#### Manual

- [x] 3.2 Empty selection → error, no dialog, no file
- [x] 3.3 Non-empty selection → dialog, file written, success message
- [x] 3.4 Zero-triangle non-empty selection → same error pattern as empty selection
- [x] 3.5 Save dialog cancel → silent abort, no error, no file
- [x] 3.6 Source document unchanged after export

### Phase 4: Host verification & roadmap fix

#### Automated

- [ ] 4.1 `dotnet build NavisworksExportPlugins.sln` succeeds (final full-solution check)
- [ ] 4.2 `roadmap.md`'s F-02 row/section no longer say `ready`; F-02 and S-03 both have `## Done` entries

#### Manual

- [x] 4.3 "Export to GLB" appears under Add-Ins and runs against a real selection on Manage 2026
- [x] 4.4 Empty selection on 2026 → error, no dialog, no file (live confirmation)
- [x] 4.5 Resulting `.glb` renders correctly and interactively in PowerPoint
- [x] 4.6 Large/instanced selection exports without hanging on Manage 2026
- [ ] 4.7 Zero-triangle selection → same error pattern, live on 2026
- [x] 4.8 Original Manage 2026 document unchanged after the full run
- [ ] 4.9 Manage 2023 plugin still loads and works unaffected (regression sanity)
