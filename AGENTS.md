# Repository Guidelines

Navisworks Manage **2026** desktop export plugins (GLB for PowerPoint 3D, AutoCAD DWG/DXF) in C# / .NET Framework 4.8 — see `@context/foundation/tech-stack.md` and `@context/foundation/prd.md`.

## Hard rules

- Export **current selection only** — never the whole model (`@context/foundation/prd.md`).
- Plugins are **read-only** on the Navisworks document; do not mutate the source model.
- Host is **Navisworks Manage 2026** only. **Manage 2023** and **Manage 2025** are out of scope.
- Empty selection → user-facing error; do not write an empty export file.
- No cloud sync, auth, backend, or commercial packaging in MVP.
- Preserve `context/` across reshapes; product docs live under `@context/foundation/`.

## Project structure

- `NavisworksExportPlugins.sln` — solution entry point (Manage 2026 plugins only).
- `NavisworksExport.Geometry.2026/` — shared COM geometry extraction (`AssemblyName` `NavisworksExport.Geometry.2026`).
- `NavisworksExport.Glb.2026/` — GLB export plugin (`AssemblyName` `NavisworksExport.Glb.2026`).
- `NavisworksExport.AutoCad.2026/` — AutoCAD DWG/DXF export plugin (`AssemblyName` `NavisworksExport.AutoCad.2026`).
- `Directory.Build.props` — shared `NavisworksInstallDir` (Manage 2026); override via env/MSBuild property of the same name.
- `@context/foundation/` — PRD, tech-stack, shape-notes (edit in place; see `@context/foundation/README.md`).
- `@context/changes/` — per-change plans/research; archived work under `@context/archive/`.
- `.cursor/skills/` and `.cursor/rules/` — agent workflows; not runtime code.

## Build and development

- `dotnet restore` — restore NuGet packages for the solution.
- `dotnet build NavisworksExportPlugins.sln` — compile `net48` / x64 plugin projects.
- Require a local Manage 2026 install — HintPath from `NavisworksInstallDir` only (no NuGet API fallback). Missing install causes the build to fail on the API reference.
- Post-build deploy copies each DLL into `{NavisworksInstallDir}Plugins\<AssemblyName>\`. Those paths are under Program Files — use an elevated terminal/IDE for the copy to succeed. Non-elevated builds still exit 0 (deploy warns and continues).
- To debug a command inside the host, launch `$(NavisworksInstallDir)Roamer.exe`.

## Coding style

- C# with nullable reference types (`net48`, `LangVersion` latest via `Directory.Build.props`).
- Prefer `AddInPlugin` command entry points; keep plugins independent until real shared logic appears.
- Put durable decisions in foundation docs via `@`-references; do not duplicate long specs in code comments.

## Manage 2026 plugin checklist

When wiring an export command, read `@context/foundation/lessons.md` first. Minimum for every Add-In that references private DLLs:

1. **`PluginAssemblyResolver`** in `Execute` before any JIT of writer/geometry types — copy from `NavisworksExport.Glb.2026/GlbExportCommand.cs`.
2. **`Execute` → `[NoInlining] RunExport`** + try/catch + temp-file log (`%TEMP%\NavisworksExport.<Plugin>.2026.log`).
3. **Multi-DLL deploy** — post-build copies all `*.dll` + `*.pdb` except `Autodesk.*.dll` into `{NavisworksInstallDir}Plugins\<AssemblyName>\` (elevated build required).
4. **Explicit `Autodesk.Navisworks.ComApi` reference** when the command uses geometry extraction.
5. **Shared geometry** — COM callback is already `public`/`[ComVisible]`; matrix, leaf expansion, and `frag.Geometry` guards live in `NavisworksExport.Geometry.2026` — do not reimplement or bypass them in new extract APIs.

Format-specific notes (axis swap, materials, DWG viewport shading) belong in the change plan, not here.

## Testing

No test project or runner is configured yet. When adding tests, use a `*.Tests` project (or co-located tests) and document the single-test command here. Host-load verification is manual inside Navisworks Manage 2026.

## Commits and PRs

Conventional Commits preferred (`feat` / `fix` / `chore` / …). Intended CI is GitHub Actions (`@context/foundation/tech-stack.md`); no workflow exists yet.
