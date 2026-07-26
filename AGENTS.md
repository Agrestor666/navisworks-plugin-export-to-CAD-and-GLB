# Repository Guidelines

Navisworks Manage desktop export plugins (GLB for PowerPoint 3D, AutoCAD DWG/DXF) in C# / .NET Framework 4.8. Manage **2023** is the MVP host; Manage **2026** twin Add-In projects share the same root solution — see `@context/foundation/tech-stack.md` and `@context/foundation/prd.md`.

## Hard rules

- Export **current selection only** — never the whole model (`@context/foundation/prd.md`).
- Plugins are **read-only** on the Navisworks document; do not mutate the source model.
- MVP host is **Navisworks Manage 2023**; next host wave is **Manage 2026** (twin `*.2026` projects). **Manage 2025** is out of scope (parked — no install).
- Empty selection → user-facing error; do not write an empty export file.
- No cloud sync, auth, backend, or commercial packaging in MVP.
- Preserve `context/` across reshapes; product docs live under `@context/foundation/`.

## Project structure

- `NavisworksExportPlugins.sln` — solution entry point for 2023 + 2026 plugins.
- `NavisworksExport.Glb/` — GLB export plugin for Manage 2023 (`AddInPlugin` command).
- `NavisworksExport.AutoCad/` — AutoCAD DWG/DXF export plugin for Manage 2023 (`AddInPlugin` command).
- `NavisworksExport.Glb.2026/` — Manage 2026 twin of the GLB plugin (`AssemblyName` `NavisworksExport.Glb.2026`).
- `NavisworksExport.AutoCad.2026/` — Manage 2026 twin of the AutoCAD plugin (`AssemblyName` `NavisworksExport.AutoCad.2026`).
- `Directory.Build.props` — shared `NavisworksInstallDir` (Manage 2023) and `NavisworksInstallDir2026` (Manage 2026); override either via env/MSBuild property of the same name.
- `@context/foundation/` — PRD, tech-stack, shape-notes (edit in place; see `@context/foundation/README.md`).
- `@context/changes/` — per-change plans/research; archived work under `@context/archive/`.
- `.cursor/skills/` and `.cursor/rules/` — agent workflows; not runtime code.

## Build and development

- `dotnet restore` — restore NuGet packages for the solution.
- `dotnet build NavisworksExportPlugins.sln` — compile `net48` / x64 plugin projects (2023 + 2026).
- **2023 projects:** Prefer a machine with Navisworks Manage 2023 installed so `Autodesk.Navisworks.Api.dll` resolves via `HintPath` from `NavisworksInstallDir`. Without a local install, they fall back to the `NavisworksAPIdlls2023` NuGet package for compile only.
- **2026 projects:** Require a local Manage 2026 install — HintPath from `NavisworksInstallDir2026` only (no NuGet API fallback). Missing install causes the build to fail on the API reference.
- Post-build deploy copies each DLL into `{NavisworksInstallDir}Plugins\<AssemblyName>\` (2023) or `{NavisworksInstallDir2026}Plugins\<AssemblyName>\` (2026). Those paths are under Program Files — use an elevated terminal/IDE for the copy to succeed. Non-elevated builds still exit 0 (deploy warns and continues).
- To debug a command inside the host, launch the matching host’s `Roamer.exe`: `$(NavisworksInstallDir)Roamer.exe` for 2023 projects, `$(NavisworksInstallDir2026)Roamer.exe` for 2026 projects.

## Coding style

- C# with nullable reference types (`net48`, `LangVersion` latest via `Directory.Build.props`).
- Prefer `AddInPlugin` command entry points; keep plugins independent until real shared logic appears.
- Put durable decisions in foundation docs via `@`-references; do not duplicate long specs in code comments.

## Manage 2026 plugin checklist

When wiring or porting a `*.2026` export command, read `@context/foundation/lessons.md` first (S-03 GLB pitfalls). Minimum for every 2026 Add-In that references private DLLs:

1. **`PluginAssemblyResolver`** in `Execute` before any JIT of writer/geometry types — copy from `NavisworksExport.Glb.2026/GlbExportCommand.cs`.
2. **`Execute` → `[NoInlining] RunExport`** + try/catch + temp-file log (`%TEMP%\NavisworksExport.<Plugin>.2026.log`).
3. **Multi-DLL deploy** — post-build copies all `*.dll` + `*.pdb` except `Autodesk.*.dll` into `{NavisworksInstallDir2026}Plugins\<AssemblyName>\` (elevated build required).
4. **Explicit `Autodesk.Navisworks.ComApi` reference** when the command uses geometry extraction.
5. **Shared geometry** — COM callback is already `public`/`[ComVisible]`; matrix, leaf expansion, and 2026 `frag.Geometry` guards live in linked `NavisworksExport.Geometry` source — do not reimplement or bypass them in new extract APIs.

Format-specific notes (axis swap, materials, DWG viewport shading) belong in the change plan, not here.

## Testing

No test project or runner is configured yet. When adding tests, use a `*.Tests` project (or co-located tests) and document the single-test command here. Host-load verification is manual inside Navisworks Manage 2023 and Manage 2026 as applicable.

## Commits and PRs

Conventional Commits preferred (`feat` / `fix` / `chore` / …). Intended CI is GitHub Actions (`@context/foundation/tech-stack.md`); no workflow exists yet.
