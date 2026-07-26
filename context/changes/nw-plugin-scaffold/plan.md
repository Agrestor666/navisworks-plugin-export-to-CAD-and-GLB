# Navisworks Plugin Host Scaffold — Implementation Plan

## Overview

Reshape the temporary ASP.NET Core webapi scaffold into a real, loadable Navisworks Manage 2023 plugin host: two independent `.NET Framework 4.8` class library projects (GLB export, AutoCAD export), each exposing a stub `AddInPlugin` command that Navisworks can load from its Add-Ins tab. Each stub proves the one API surface both future export slices (S-01, S-02) depend on — reading the current read-only selection — without implementing any actual export logic.

## Current State Analysis

The repo root currently holds a disposable ASP.NET Core webapi scaffold (`Program.cs`, `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `appsettings*.json`, `Properties/launchSettings.json`) targeting `net9.0`. This was intentionally a placeholder (`context/changes/bootstrap-verification/verification.md`) — the registry had no Navisworks-specific starter, so the closest generic `.NET` card was bootstrapped with the explicit expectation it would be reshaped. There is no git history yet, no test project, and no code that talks to the Navisworks API.

Navisworks Manage 2023 add-ins are architecturally incompatible with that scaffold: they must target **.NET Framework 4.8** (x64), not `net9.0` — loading a modern .NET assembly crashes the host. Navisworks has no manifest-file mechanism (unlike Revit's `.addin` XML); it loads plugins purely by folder/assembly-name convention from `{Install}\Plugins\<AssemblyName>\<AssemblyName>.dll`. Entry points are plain C# classes deriving from types in `Autodesk.Navisworks.Api.Plugins` (most commonly `AddInPlugin`), decorated with `[Plugin]` and `[AddInPlugin]` attributes.

## Desired End State

Two buildable `.NET Framework 4.8` plugin projects exist at the repo root (`NavisworksExport.Glb`, `NavisworksExport.AutoCad`), each producing an assembly that Navisworks Manage 2023 can load and list under its Add-Ins tab. Each exposes one command ("Export to GLB" / "Export to AutoCAD") whose `Execute()` reads the current selection count via the read-only Navisworks API and displays it — proving the host loads the plugin, the command fires, and the selection API is reachable without mutating the source document. The repo has its own git history from this point forward. Verified by: `dotnet build` succeeding on the new solution, and manually confirming both commands run correctly inside a live Navisworks Manage 2023 session.

### Key Discoveries:

- Navisworks add-ins require `net48`, platform `x64` — confirmed via the community `NavisworksAPIdlls2023` NuGet package metadata and multiple Autodesk Community threads describing host crashes on modern .NET.
- Plugin discovery is folder-name-exact-match, not manifest-based: `Abc.Def.dll` must sit in `Plugins\Abc.Def\Abc.Def.dll` (`ApiDocs.co · Navisworks · Plug-ins`).
- `AddInPlugin` is the Autodesk-recommended first plugin type to implement; it shows in the "Add-Ins" ribbon tab and is driven by a single `Execute(params string[])` method returning an `int`.
- The standard local dev workflow is a post-build copy into `{Install}\Plugins\<AssemblyName>\`, which requires an elevated build (the target folder lives under `Program Files`) — this is a known friction point across every community example found, not something specific to this repo.
- `AGENTS.md` already documents this scaffold as disposable ("replace it when plugin projects land") and flags `git init` as still pending — both are addressed by this plan.

## What We're NOT Doing

- Implementing any real export logic (glTF/GLB writing, DWG/DXF writing) — that's `S-01` (`export-selection-glb`) and `S-02` (`export-selection-autocad`).
- The empty-selection user-facing error dialog (FR-007) or the save-file dialog (FR-008) — the scaffold's stub tolerates an empty selection (shows count `0`) without crashing, but doesn't implement the PRD's error-message requirement.
- A shared core class library between the two plugin projects — explicitly deferred; each plugin project is fully independent for now, factor out shared code only when S-01/S-02 reveal real duplication.
- Navisworks Manage 2025 support — MVP targets 2023 only per PRD Non-Goals.
- A test project — no business logic exists yet to test; deferred to S-01/S-02.
- CI/CD (GitHub Actions) — parked in the roadmap under `main_goal: speed`.
- Packaging as an Autodesk `.bundle` for admin-rights-free / multi-user installation — local dev-machine sideloading via the Plugins folder is sufficient for this internal tool.
- Ribbon icons, tooltips polish, or custom panel/dockable UI — the default Add-Ins tab entry is sufficient to prove loadability.

## Implementation Approach

Two independent SDK-style `net48` class library projects (one per future plugin), tied together by a root `.sln` and a shared `Directory.Build.props` that centralizes the one thing they truly share: how to find the Navisworks install on the local machine. Each project references only `Autodesk.Navisworks.Api.dll` (the minimum needed for `Application.ActiveDocument.CurrentSelection`) plus the framework's `System.Windows.Forms` for the proof-of-life `MessageBox`. A post-build MSBuild target deploys each project's output into the local Navisworks Plugins folder so the loop of "build → open Navisworks → see it load" is one step, without making the build itself depend on elevated permissions to succeed.

## Critical Implementation Details

- **Elevated build required for host deploy**: the post-build deploy target writes into `$(NavisworksInstallDir)Plugins\...`, which lives under `Program Files`. Every reference implementation found needs an elevated terminal/IDE for this step to actually copy files. The target is written with `ContinueOnError="WarnAndContinue"` on each step specifically so `dotnet build` still exits `0` from a normal terminal — the deploy just silently no-ops without elevation, and the developer re-runs elevated when they want the copy to land.
- **Debugging against the live host**: to hit breakpoints while a command runs inside Navisworks, set the project's debug launch external program to `$(NavisworksInstallDir)Roamer.exe` (Navisworks Manage's executable) in Visual Studio's project debug settings — this isn't discoverable from the file layout alone and is the standard way Navisworks plugin authors debug locally.

## Phase 1: Repository foundation

### Overview

Give the repo real git history and remove the disposable webapi scaffold files that the Navisworks plugin projects will replace.

### Changes Required:

#### 1. Git repository initialization

**File**: (repository root)

**Intent**: Start version-controlled history now, before the scaffold deletion/recreation in this change happens — otherwise the entire reshape has no diff trail.

**Contract**: Run `git init` at the repo root; no branch/remote configuration beyond the default.

#### 2. `.gitignore`

**File**: `.gitignore`

**Intent**: Keep build artifacts (`bin/`, `obj/`, `.vs/`) and per-developer IDE state out of version control from the first commit.

**Contract**: Standard Visual Studio / .NET `.gitignore` content (e.g. the GitHub `VisualStudio.gitignore` template) — must cover `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`.

#### 3. Remove obsolete webapi scaffold

**File**: `Program.cs`, `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `appsettings.json`, `appsettings.Development.json`, `Properties/` (contains `launchSettings.json`), `obj/`

**Intent**: These files are the disposable ASP.NET Core placeholder called out in `AGENTS.md` and `context/changes/bootstrap-verification/verification.md` — they have no role once real plugin projects exist and would otherwise sit alongside the new projects as dead weight.

**Contract**: Delete all listed files/directories. No replacement content in this phase — Phase 2 creates the real project files.

### Success Criteria:

#### Automated Verification:

- `git rev-parse --is-inside-work-tree` succeeds (repo is initialized)
- Obsolete scaffold files no longer exist on disk: `Program.cs`, `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Plugin project scaffolding

### Overview

Create the two `net48`/x64 plugin projects, the shared install-path configuration, and the solution file tying them together — no plugin-specific code yet, just a host-loadable build shape.

### Changes Required:

#### 1. Shared install-path configuration

**File**: `Directory.Build.props` (repo root)

**Intent**: Centralize the one machine-specific fact both plugin projects need — where Navisworks Manage 2023 is installed — as a single overridable MSBuild property instead of duplicating a hardcoded path in each `.csproj`.

**Contract**: Defines `NavisworksInstallDir` with a standard default, overridable by a pre-set environment variable of the same name:

```xml
<Project>
  <PropertyGroup>
    <NavisworksInstallDir Condition="'$(NavisworksInstallDir)' == ''">C:\Program Files\Autodesk\Navisworks Manage 2023\</NavisworksInstallDir>
  </PropertyGroup>
</Project>
```

#### 2. GLB plugin project

**File**: `NavisworksExport.Glb/NavisworksExport.Glb.csproj`

**Intent**: A minimal loadable plugin project for the future GLB export command — SDK-style so `dotnet build`/`dotnet restore` keep working exactly as `AGENTS.md` already documents, but targeting the framework Navisworks actually requires.

**Contract**: SDK-style project, `TargetFramework=net48`, `PlatformTarget=x64`, references `System.Windows.Forms` (framework reference) and `Autodesk.Navisworks.Api.dll` via `HintPath="$(NavisworksInstallDir)Autodesk.Navisworks.Api.dll"` with `Private=False` (Navisworks already provides this assembly at runtime from its own directory — copying a local copy is unnecessary and risks version mismatch). Includes a post-build `Target` that deploys the build output to `$(NavisworksInstallDir)Plugins\$(AssemblyName)\`, matching Navisworks' folder-name-must-equal-assembly-name loading convention, with every step set to `ContinueOnError="WarnAndContinue"` per the Critical Implementation Details note above:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <AssemblyName>NavisworksExport.Glb</AssemblyName>
    <RootNamespace>NavisworksExport.Glb</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System.Windows.Forms" />
    <Reference Include="Autodesk.Navisworks.Api">
      <HintPath>$(NavisworksInstallDir)Autodesk.Navisworks.Api.dll</HintPath>
      <Private>False</Private>
      <SpecificVersion>False</SpecificVersion>
    </Reference>
  </ItemGroup>

  <Target Name="DeployToNavisworksPlugins" AfterTargets="Build">
    <RemoveDir Directories="$(NavisworksInstallDir)Plugins\$(AssemblyName)" Condition="Exists('$(NavisworksInstallDir)Plugins\$(AssemblyName)')" ContinueOnError="WarnAndContinue" />
    <MakeDir Directories="$(NavisworksInstallDir)Plugins\$(AssemblyName)" ContinueOnError="WarnAndContinue" />
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(NavisworksInstallDir)Plugins\$(AssemblyName)" ContinueOnError="WarnAndContinue" />
  </Target>

</Project>
```

#### 3. AutoCAD plugin project

**File**: `NavisworksExport.AutoCad/NavisworksExport.AutoCad.csproj`

**Intent**: Same role as the GLB project, for the future AutoCAD export command — an independent assembly so it can be installed/uninstalled/shipped separately from the GLB plugin.

**Contract**: Identical shape to `NavisworksExport.Glb.csproj` above, with `AssemblyName`/`RootNamespace` set to `NavisworksExport.AutoCad`.

#### 4. Solution file

**File**: `NavisworksExportPlugins.sln` (repo root)

**Intent**: Give the two projects a single entry point for `dotnet build`/IDE loading, matching `AGENTS.md`'s existing `dotnet restore` / `dotnet build` workflow.

**Contract**: Standard `.sln` referencing both `.csproj` files (create via `dotnet new sln` + `dotnet sln add`).

### Success Criteria:

#### Automated Verification:

- `dotnet restore` succeeds for the solution
- `dotnet build NavisworksExportPlugins.sln` succeeds (exit code 0), producing `net48` assemblies for both `NavisworksExport.Glb` and `NavisworksExport.AutoCad`
- The same build command still exits `0` when run from a non-elevated terminal (deploy-copy failures must warn, not fail the build)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Command entry points

### Overview

Add the actual `AddInPlugin` command classes — the thin layer that makes each project a real, ribbon-visible Navisworks command instead of just a class library that happens to reference the Navisworks API.

### Changes Required:

#### 1. GLB export command stub

**File**: `NavisworksExport.Glb/GlbExportCommand.cs`

**Intent**: Prove the full path — Navisworks loads the assembly, shows the command in the Add-Ins tab, `Execute()` fires, and the read-only `CurrentSelection` API is reachable — without touching export format concerns.

**Contract**: A public class deriving from `Autodesk.Navisworks.Api.Plugins.AddInPlugin`, decorated with `[Plugin(id, developerId, ...)]` and `[AddInPlugin(AddInLocation.AddIn)]`, whose `Execute` reads `Application.ActiveDocument.CurrentSelection.SelectedItems.Count` and displays it via `MessageBox.Show` — never writes to the document:

```csharp
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisworksExport.Glb
{
    [Plugin("GlbExport", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class GlbExportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var count = Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count ?? 0;
            MessageBox.Show($"Selected items: {count}", "Export to GLB (scaffold)");
            return 0;
        }
    }
}
```

#### 2. AutoCAD export command stub

**File**: `NavisworksExport.AutoCad/AutoCadExportCommand.cs`

**Intent**: Same proof as the GLB command, for the AutoCAD plugin's independent assembly.

**Contract**: Identical shape to `GlbExportCommand`, with `Plugin` id `"AutoCadExport"`, same `"NWXP"` developer id, `DisplayName = "Export to AutoCAD"`, dialog title `"Export to AutoCAD (scaffold)"`.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with both command classes compiled in

#### Manual Verification:

- An elevated build successfully deploys both DLLs into `{NavisworksInstallDir}\Plugins\<AssemblyName>\`, and the folder name exactly matches the assembly name in each case

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Host verification & docs

### Overview

Confirm the plugins actually load and run correctly inside a live Navisworks Manage 2023 session, and bring `AGENTS.md` up to date with the new project shape so the next change (`S-01`/`S-02`) starts from accurate onboarding docs.

### Changes Required:

#### 1. `AGENTS.md` refresh

**File**: `AGENTS.md`

**Intent**: The current "Build and development" and "Project structure" sections describe the now-deleted webapi scaffold; the "Hard rules" bullet about the weatherforecast sample is now moot since `Program.cs` no longer exists. Bring the doc in line with reality so it stays trustworthy for the next change.

**Contract**: Replace the webapi-specific `dotnet restore`/`dotnet build`/`dotnet run` bullets with the plugin solution's build commands (`dotnet restore`, `dotnet build NavisworksExportPlugins.sln`) plus a short note on the elevated-deploy and Roamer.exe debug-launch behaviors from Critical Implementation Details. Update "Project structure" to describe `NavisworksExport.Glb/`, `NavisworksExport.AutoCad/`, `Directory.Build.props`, and `NavisworksExportPlugins.sln` in place of `Program.cs`/`appsettings*.json`/`Properties/`. Remove the now-stale "Do not treat the webapi weatherforecast sample... as product code" hard rule.

### Success Criteria:

#### Automated Verification:

- `AGENTS.md` no longer references `.bootstrap-scaffold.csproj`, `Program.cs`, or the weatherforecast sample

#### Manual Verification:

- Navisworks Manage 2023 launches normally (no Autodesk error-report crash dialog) after both plugins are deployed
- Both "Export to GLB" and "Export to AutoCAD" commands appear under the Navisworks Add-Ins tab
- Running each command with a selection made shows a message box with the correct selected-item count
- Running each command with an empty selection shows "Selected items: 0" without crashing
- The Navisworks document's modified/unsaved-changes indicator is unchanged after running either command, confirming the plugin never mutates the source model

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None in this change — no business logic exists yet to unit test. A test project is deferred to `S-01`/`S-02` once there's export logic worth covering.

### Integration Tests:

- None automatable outside a real Navisworks session; all host-integration verification is manual (see per-phase Manual Verification and Manual Testing Steps below).

### Manual Testing Steps:

1. Run an elevated `dotnet build NavisworksExportPlugins.sln` so the post-build deploy step can write into `Program Files`.
2. Launch Navisworks Manage 2023 and open any model.
3. Open the Add-Ins tab; confirm both "Export to GLB" and "Export to AutoCAD" commands are listed.
4. Select a handful of objects in the model tree, run "Export to GLB", confirm the message box reports the correct count.
5. Repeat step 4 for "Export to AutoCAD".
6. Clear the selection entirely, run each command again, confirm both show "Selected items: 0" without an error dialog or crash.
7. Confirm the model's unsaved-changes indicator is unaffected by any of the above runs.

## Performance Considerations

None — each stub command does a single in-memory collection count and a synchronous message box; no measurable performance surface exists at this scaffold stage.

## Migration Notes

This is a from-scratch reshape, not a data migration: the disposable webapi scaffold holds no user data or configuration worth preserving. Phase 1 deletes it outright before Phase 2 creates the real plugin projects in its place.

## References

- Roadmap: `context/foundation/roadmap.md` — `F-01: nw-plugin-scaffold`
- PRD guardrails: `context/foundation/prd.md` — "Plugin nie modyfikuje oryginalnego modelu Navisworks", NFR (NW 2023 support)
- Tech stack hand-off: `context/foundation/tech-stack.md`
- Bootstrap verification log: `context/changes/bootstrap-verification/verification.md`
- Navisworks plug-in architecture: ApiDocs.co · Navisworks · Plug-ins (folder/assembly-name loading convention, `AddInPlugin` recommendation)
- Target framework constraint: `NavisworksAPIdlls2023` NuGet package metadata (net48); Autodesk Community threads on host crashes with modern .NET
- Post-build deploy pattern: TwentyTwo.space "Navisworks API: Creating Navisworks Add-Ins"; community `NavisAddinManager` / `NetPluginPropertyDatabaseExample` project files

## Implementation Addendum

Agreed during implement (dev machine had no local Navisworks Manage 2023 install):

1. **NuGet API fallback** — both `.csproj` files prefer `HintPath` to `$(NavisworksInstallDir)Autodesk.Navisworks.Api.dll` when present; otherwise reference `NavisworksAPIdlls2023` (PrivateAssets/ExcludeAssets runtime) so `dotnet build` works off-host.
2. **`LangVersion=latest`** in `Directory.Build.props` — required so `<Nullable>enable</Nullable>` compiles under `net48` (default C# 7.3 rejects it).
3. **`NwApplication` type alias** in both command stubs — resolves the `Application` name clash between `Autodesk.Navisworks.Api` and `System.Windows.Forms`.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Repository foundation

#### Automated

- [x] 1.1 `git rev-parse --is-inside-work-tree` succeeds — 418da57
- [x] 1.2 Obsolete scaffold files no longer exist on disk — 418da57

### Phase 2: Plugin project scaffolding

#### Automated

- [x] 2.1 `dotnet restore` succeeds for the solution — 692e139
- [x] 2.2 `dotnet build NavisworksExportPlugins.sln` succeeds, producing net48 assemblies for both projects — 692e139
- [x] 2.3 Build still exits 0 from a non-elevated terminal — 692e139

### Phase 3: Command entry points

#### Automated

- [x] 3.1 `dotnet build` succeeds with both command classes compiled in — 19c2794

#### Manual

- [x] 3.2 Elevated build deploys both DLLs into `{NavisworksInstallDir}\Plugins\<AssemblyName>\` with matching folder/DLL names — 19c2794

### Phase 4: Host verification & docs

#### Automated

- [x] 4.1 `AGENTS.md` no longer references `.bootstrap-scaffold.csproj`, `Program.cs`, or the weatherforecast sample — 32204dd

#### Manual

- [x] 4.2 Navisworks Manage 2023 launches normally after deploy — 32204dd
- [x] 4.3 Both commands appear under the Add-Ins tab — 32204dd
- [x] 4.4 Each command shows the correct selected-item count with a selection made — 32204dd
- [x] 4.5 Each command shows "Selected items: 0" on an empty selection without crashing — 32204dd
- [x] 4.6 Document's unsaved-changes indicator is unchanged after running either command — 32204dd
