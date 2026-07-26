# Navisworks Manage 2026 Plugin Host Scaffold — Implementation Plan

## Overview

Add the smallest loadable **Manage 2026** twin of the proven F-01 host: two independent `net48`/x64 plugin projects (`NavisworksExport.Glb.2026`, `NavisworksExport.AutoCad.2026`) with stub `AddInPlugin` commands, a separate `NavisworksInstallDir2026` MSBuild property, post-build deploy into the 2026 Plugins folder, live host verification, and foundation docs updated from “2025 @ v1.1” to “2026 next wave”. This is roadmap **F-02** — no real export logic — and unblocks **S-03**.

## Current State Analysis

The repo already has a working Manage **2023** host (F-01 done, archived at `context/archive/2026-07-26-nw-plugin-scaffold/`) plus S-01 GLB export on `NavisworksExport.Glb`. Layout today:

- `NavisworksExport.Glb` — full GLB export (not a stub)
- `NavisworksExport.AutoCad` — still the F-01 selection-count stub
- `NavisworksExport.Geometry` — shared COM extraction (2023 API refs)
- `Directory.Build.props` — `NavisworksInstallDir` defaults to Manage 2023 (+ trailing-slash normalize, `LangVersion=latest`)
- 2023 projects use HintPath-first + `NavisworksAPIdlls2023` NuGet fallback for off-host compile

There is **no** 2026 project, path, or deploy target. PRD / AGENTS / tech-stack still say MVP 2023 and 2025 in v1.1; roadmap already parks 2025 and sequences F-02 → S-03 on Manage **2026**.

F-02 is a **host retarget twin**, not a second greenfield: copy the F-01 load/deploy/`AddInPlugin` pattern onto 2026 assemblies while leaving 2023 green.

## Desired End State

- Two new projects build as `NavisworksExport.Glb.2026` and `NavisworksExport.AutoCad.2026` (`net48`, x64), referencing `Autodesk.Navisworks.Api.dll` from Manage **2026** only (install required — no NuGet fallback).
- Elevated build deploys each DLL to `{NavisworksInstallDir2026}Plugins\<AssemblyName>\`.
- In Manage 2026, both Add-Ins commands appear, report selection count (including `0`), and never mutate the document.
- `AGENTS.md`, `prd.md`, and `tech-stack.md` describe 2023 MVP + **2026** as the next host wave (2025 parked / out of scope).
- Existing 2023 projects still build and deploy unchanged.

### Key Discoveries:

- F-01 proven contracts: folder name = assembly name; `AddInPlugin` + `[Plugin]`/`[AddInPlugin]`; deploy with `ContinueOnError="WarnAndContinue"`; `NwApplication` alias for WinForms clash — see `context/archive/2026-07-26-nw-plugin-scaffold/plan.md` and `NavisworksExport.AutoCad/AutoCadExportCommand.cs:1-18`.
- Do **not** copy today’s `GlbExportCommand` (S-01 export). Twin stubs must match the **AutoCAD F-01 stub** shape (selection-count MessageBox only).
- No official `NavisworksAPIdlls2026` NuGet twin; community packages exist but were rejected — 2026 projects are install-required HintPath only.
- `Directory.Build.props:3-4` must keep `NavisworksInstallDir` → 2023; add a sibling `NavisworksInstallDir2026` so 2023 builds cannot be accidentally retargeted.
- Solution already forces x64 for all projects (`NavisworksExportPlugins.sln:22-57`) — new projects need the same configuration mappings.

## What We're NOT Doing

- Real GLB / DWG / DXF export on 2026 (that’s `S-03` / `S-04`).
- Retargeting or multi-targeting existing 2023 projects / Geometry for 2026.
- NuGet / third-party API fallback for 2026 off-host builds.
- FR-007 empty-selection error / FR-008 save dialog on the 2026 stubs (same as F-01: tolerate count `0`).
- Test project, CI/CD, `.bundle` packaging, ribbon polish.
- Implementing Manage **2025** support.
- Porting S-01 Geometry/GLB writer onto 2026 inside this change.

## Implementation Approach

Mirror F-01 as **parallel projects** with a `.2026` assembly suffix so both hosts can be installed side-by-side. Centralize the 2026 install path in `Directory.Build.props` as `NavisworksInstallDir2026` (env-overridable, trailing-slash normalized). Each twin references only the 2026 host API via HintPath when the DLL exists; if missing, the build fails with a missing reference (by design). Stub commands prove load + read-only `CurrentSelection`. Final phase verifies inside Manage 2026 and closes the foundation docs gap.

## Critical Implementation Details

- **Elevated deploy**: same Program Files friction as F-01 — `ContinueOnError="WarnAndContinue"` on MakeDir/Copy; non-elevated build must still exit 0; host verification requires an elevated build so DLLs actually land under Manage 2026 `Plugins\`.
- **Overwrite-only Copy**: do **not** `RemoveDir` before Copy (F-01 impl-review lesson) — overwrite with `OverwriteReadOnlyFiles="true"`.
- **RootNamespace vs AssemblyName**: C# forbids a digit-leading namespace segment, so `RootNamespace` is `NavisworksExport.Glb2026` / `NavisworksExport.AutoCad2026` while `AssemblyName` (DLL + deploy folder) stays `NavisworksExport.Glb.2026` / `NavisworksExport.AutoCad.2026`.
- **Stub source of truth**: copy shape from `NavisworksExport.AutoCad/AutoCadExportCommand.cs`, not from the S-01 `GlbExportCommand`.
- **Debug launch**: Visual Studio external program = `$(NavisworksInstallDir2026)Roamer.exe` for the 2026 projects.

## Phase 1: Twin project scaffolding

### Overview

Add `NavisworksInstallDir2026`, two empty-of-logic `net48`/x64 plugin projects with HintPath-only API refs and deploy targets, and wire them into the solution — without breaking 2023 builds.

### Changes Required:

#### 1. Shared 2026 install-path property

**File**: `Directory.Build.props`

**Intent**: Expose a second, overridable install root for Manage 2026 so twin projects never reuse or overwrite the 2023 `NavisworksInstallDir` default.

**Contract**: Keep existing `NavisworksInstallDir` (2023) unchanged. Add `NavisworksInstallDir2026` defaulting to `C:\Program Files\Autodesk\Navisworks Manage 2026\`, overridable by env/MSBuild property of the same name, then `EnsureTrailingSlash` (same pattern as lines 3–4 today).

#### 2. GLB 2026 plugin project

**File**: `NavisworksExport.Glb.2026/NavisworksExport.Glb.2026.csproj`

**Intent**: Minimal loadable plugin shell for the future S-03 GLB command on Manage 2026 — independent assembly so it cannot collide with the 2023 GLB plugin folder.

**Contract**: SDK-style, `TargetFramework=net48`, `PlatformTarget=x64`, `Nullable=enable`, `AssemblyName` = `NavisworksExport.Glb.2026`, `RootNamespace` = `NavisworksExport.Glb2026`. Reference `System.Windows.Forms`. Reference `Autodesk.Navisworks.Api` via `HintPath="$(NavisworksInstallDir2026)Autodesk.Navisworks.Api.dll"` with `Private=False`, `SpecificVersion=False`, conditioned on `Exists(...)` **or** unconditional HintPath such that a missing install fails the build (no `NavisworksAPIdlls*` PackageReference). Post-build `DeployToNavisworksPlugins` → `$(NavisworksInstallDir2026)Plugins\$(AssemblyName)\` copying `$(TargetPath)` with `OverwriteReadOnlyFiles="true"` and `ContinueOnError="WarnAndContinue"` on MakeDir/Copy (no `RemoveDir`).

#### 3. AutoCAD 2026 plugin project

**File**: `NavisworksExport.AutoCad.2026/NavisworksExport.AutoCad.2026.csproj`

**Intent**: Same loadable shell for the future S-04 AutoCAD command on Manage 2026.

**Contract**: Identical shape to the GLB 2026 csproj, with `AssemblyName` = `NavisworksExport.AutoCad.2026`, `RootNamespace` = `NavisworksExport.AutoCad2026`.

#### 4. Solution membership

**File**: `NavisworksExportPlugins.sln`

**Intent**: One `dotnet build` entry point for 2023 + 2026 plugins.

**Contract**: Add both new projects with the same x64 ActiveCfg/Build.0 mappings used by existing projects (Any CPU / x86 solution configs still build as x64). Prefer `dotnet sln add` then verify platform mappings match neighbors.

### Success Criteria:

#### Automated Verification:

- With Manage 2026 installed at the default path (or `NavisworksInstallDir2026` set), `dotnet build NavisworksExportPlugins.sln` succeeds and produces `net48` assemblies for `NavisworksExport.Glb.2026` and `NavisworksExport.AutoCad.2026`
- The same elevated-or-not build still exits `0` from a non-elevated terminal (deploy warnings allowed)
- Existing 2023 projects still build as part of the same solution
- `Directory.Build.props` still defaults `NavisworksInstallDir` to Manage 2023

#### Manual Verification:

- Confirm neither new project’s csproj contains a `NavisworksAPIdlls*` / third-party NuGet API PackageReference

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Command entry points

### Overview

Add stub `AddInPlugin` commands so each 2026 assembly is a real Add-Ins-tab command that proves selection API reachability — F-01 proof-of-life only.

### Changes Required:

#### 1. GLB 2026 command stub

**File**: `NavisworksExport.Glb.2026/GlbExportCommand.cs`

**Intent**: Prove Manage 2026 loads the assembly, shows the command, and can read `CurrentSelection` without export logic or document mutation.

**Contract**: Public `AddInPlugin` with `[Plugin("GlbExport2026", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]` and `[AddInPlugin(AddInLocation.AddIn)]`. `Execute` uses `NwApplication` alias (`Autodesk.Navisworks.Api.Application`), reads `ActiveDocument?.CurrentSelection?.SelectedItems?.Count ?? 0`, shows `MessageBox` titled `"Export to GLB (2026 scaffold)"`, returns `0`. Mirror null-safety and alias pattern from `NavisworksExport.AutoCad/AutoCadExportCommand.cs`.

#### 2. AutoCAD 2026 command stub

**File**: `NavisworksExport.AutoCad.2026/AutoCadExportCommand.cs`

**Intent**: Same proof for the AutoCAD 2026 twin assembly.

**Contract**: Same shape with Plugin id `"AutoCadExport2026"`, DisplayName `"Export to AutoCAD"`, MessageBox title `"Export to AutoCAD (2026 scaffold)"`.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with both 2026 command classes compiled in

#### Manual Verification:

- An elevated build deploys both DLLs into `{NavisworksInstallDir2026}Plugins\<AssemblyName>\` with folder name exactly matching assembly name

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Host verification & foundation docs

### Overview

Confirm both stubs load and run inside live Manage 2026, and close the PRD/AGENTS/tech-stack gap so “next host = 2026” is the documented truth before F-02 archives.

### Changes Required:

#### 1. `AGENTS.md` host-wave refresh

**File**: `AGENTS.md`

**Intent**: Agents must not keep treating “2025 @ v1.1” as the next host or omit the 2026 twin projects / `NavisworksInstallDir2026` knob.

**Contract**: Update Hard rules so MVP remains Manage **2023**, next host wave is Manage **2026**, and **2025** is out of scope (parked — no install). Extend Project structure with the two `*.2026` project folders and document both install-dir properties. Extend Build and development: 2023 keeps NuGet fallback; **2026 projects require a local Manage 2026 install** (HintPath only); deploy paths for both hosts; debug via the matching host’s `Roamer.exe`. Testing line may mention host-load on 2023 and 2026 as applicable.

#### 2. `prd.md` NFR / Non-Goals sync

**File**: `context/foundation/prd.md`

**Intent**: Align product NFR/Non-Goals with the operational host wave already in the roadmap.

**Contract**: Where NFR/Success Criteria/Non-Goals say Manage **2025** in v1.1, rewrite to Manage **2026** as the post-MVP host wave (2025 skipped / not planned). Keep MVP = Manage **2023**. Do not invent new user stories for 2026 in this change — same capabilities, new host.

#### 3. `tech-stack.md` hand-off note

**File**: `context/foundation/tech-stack.md`

**Intent**: Starter hand-off still implies “reshape to NW 2023 only”; note dual-host (2023 + 2026 twins) so bootstrap assumptions aren’t re-applied wrongly.

**Contract**: Short addition that the living shape is Manage 2023 plugins plus Manage 2026 twin projects (`*.2026`), still `net48`/x64 Add-Ins — not a return to the webapi scaffold.

### Success Criteria:

#### Automated Verification:

- `AGENTS.md` mentions `NavisworksInstallDir2026` and the two `*.2026` project folders, and no longer states that 2025 is the next in-scope host wave
- `prd.md` NFR/Non-Goals no longer promise Manage 2025 as v1.1; they name Manage 2026 as the next host wave (MVP remains 2023)
- `tech-stack.md` acknowledges the 2026 twin host projects

#### Manual Verification:

- Navisworks Manage 2026 launches normally after both 2026 plugins are deployed (no host crash dialog)
- Both “Export to GLB” and “Export to AutoCAD” commands from the **2026** assemblies appear under the Add-Ins tab
- Each command with a selection shows the correct selected-item count
- Each command with an empty selection shows “Selected items: 0” without crashing
- Document unsaved-changes / modified indicator is unchanged after running either command

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None — stubs only; no export business logic in F-02.

### Integration Tests:

- None automatable outside the host; host-integration is manual (Phase 3).

### Manual Testing Steps:

1. Ensure Manage 2026 is installed (or set `NavisworksInstallDir2026`).
2. Elevated `dotnet build NavisworksExportPlugins.sln`.
3. Confirm DLLs under `{NavisworksInstallDir2026}Plugins\NavisworksExport.Glb.2026\` and `...\NavisworksExport.AutoCad.2026\`.
4. Launch Manage 2026, open a model, open Add-Ins — both commands listed.
5. Select objects → run each command → correct count.
6. Clear selection → each shows `0`, no crash.
7. Confirm document not marked dirty/mutated.
8. Smoke: 2023 plugins still present/usable under Manage 2023 if that host is also installed (regression sanity).

## Performance Considerations

None — stubs do a single in-memory count + MessageBox.

## Migration Notes

Additive only. No migration of 2023 assemblies. Side-by-side install via distinct `.2026` assembly/folder names. Machines without Manage 2026 cannot compile the new projects until the product is installed or `NavisworksInstallDir2026` points at a tree that contains `Autodesk.Navisworks.Api.dll`.

## References

- Roadmap F-02: `context/foundation/roadmap.md` (Unlocks S-03)
- Archived F-01 plan: `context/archive/2026-07-26-nw-plugin-scaffold/plan.md`
- Stub pattern: `NavisworksExport.AutoCad/AutoCadExportCommand.cs`
- Shared props today: `Directory.Build.props`
- PRD NFR / Non-Goals (pre-change): `context/foundation/prd.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Twin project scaffolding

#### Automated

- [x] 1.1 With Manage 2026 available, `dotnet build NavisworksExportPlugins.sln` succeeds and produces net48 assemblies for both `*.2026` projects — 2792c46
- [x] 1.2 Build still exits 0 from a non-elevated terminal — 2792c46
- [x] 1.3 Existing 2023 projects still build in the same solution — 2792c46
- [x] 1.4 `Directory.Build.props` still defaults `NavisworksInstallDir` to Manage 2023 — 2792c46

#### Manual

- [x] 1.5 Neither new csproj contains a `NavisworksAPIdlls*` / third-party NuGet API PackageReference — 2792c46

### Phase 2: Command entry points

#### Automated

- [x] 2.1 `dotnet build` succeeds with both 2026 command classes compiled in — bfc5645

#### Manual

- [x] 2.2 Elevated build deploys both DLLs into `{NavisworksInstallDir2026}Plugins\<AssemblyName>\` with matching folder/DLL names — bfc5645

### Phase 3: Host verification & foundation docs

#### Automated

- [x] 3.1 `AGENTS.md` documents `NavisworksInstallDir2026` + `*.2026` projects; 2025 is not the next in-scope host — 375901e
- [x] 3.2 `prd.md` names Manage 2026 as next host wave (MVP remains 2023); no 2025-as-v1.1 promise — 375901e
- [x] 3.3 `tech-stack.md` acknowledges 2026 twin host projects — 375901e

#### Manual

- [x] 3.4 Manage 2026 launches normally after 2026 plugin deploy — 375901e
- [x] 3.5 Both 2026 commands appear under Add-Ins — 375901e
- [x] 3.6 Each command shows correct count with a selection — 375901e
- [x] 3.7 Each command shows “Selected items: 0” on empty selection without crashing — 375901e
- [x] 3.8 Document unsaved-changes indicator unchanged after either command — 375901e
