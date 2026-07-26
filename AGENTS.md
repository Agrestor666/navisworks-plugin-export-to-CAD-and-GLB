# Repository Guidelines

Navisworks Manage 2023 desktop export plugins (GLB for PowerPoint 3D, AutoCAD DWG/DXF) in C# / .NET Framework 4.8. Two independent Add-In plugins share a root solution — see `@context/foundation/tech-stack.md` and `@context/foundation/prd.md`.

## Hard rules

- Export **current selection only** — never the whole model (`@context/foundation/prd.md`).
- Plugins are **read-only** on the Navisworks document; do not mutate the source model.
- MVP host is **Navisworks Manage 2023** only; 2025 is out of scope until v1.1.
- Empty selection → user-facing error; do not write an empty export file.
- No cloud sync, auth, backend, or commercial packaging in MVP.
- Preserve `context/` across reshapes; product docs live under `@context/foundation/`.

## Project structure

- `NavisworksExportPlugins.sln` — solution entry point for both plugins.
- `NavisworksExport.Glb/` — GLB export plugin (`AddInPlugin` command).
- `NavisworksExport.AutoCad/` — AutoCAD DWG/DXF export plugin (`AddInPlugin` command).
- `Directory.Build.props` — shared `NavisworksInstallDir` (default install path; override via env var of the same name).
- `@context/foundation/` — PRD, tech-stack, shape-notes (edit in place; see `@context/foundation/README.md`).
- `@context/changes/` — per-change plans/research; archived work under `@context/archive/`.
- `.cursor/skills/` and `.cursor/rules/` — agent workflows; not runtime code.

## Build and development

- `dotnet restore` — restore NuGet packages for the solution.
- `dotnet build NavisworksExportPlugins.sln` — compile both `net48` / x64 plugin projects.
- Prefer a machine with Navisworks Manage 2023 installed so `Autodesk.Navisworks.Api.dll` resolves via `HintPath` from `NavisworksInstallDir`. Without a local install, projects fall back to the `NavisworksAPIdlls2023` NuGet package for compile only.
- Post-build deploy copies each DLL into `{NavisworksInstallDir}Plugins\<AssemblyName>\`. That path is under Program Files — use an elevated terminal/IDE for the copy to succeed. Non-elevated builds still exit 0 (deploy warns and continues).
- To debug a command inside the host, launch external program `$(NavisworksInstallDir)Roamer.exe` from Visual Studio project debug settings.

## Coding style

- C# with nullable reference types (`net48`, `LangVersion` latest via `Directory.Build.props`).
- Prefer `AddInPlugin` command entry points; keep plugins independent until real shared logic appears.
- Put durable decisions in foundation docs via `@`-references; do not duplicate long specs in code comments.

## Testing

No test project or runner is configured yet. When adding tests, use a `*.Tests` project (or co-located tests) and document the single-test command here. Host-load verification is manual inside Navisworks Manage 2023.

## Commits and PRs

Conventional Commits preferred (`feat` / `fix` / `chore` / …). Intended CI is GitHub Actions (`@context/foundation/tech-stack.md`); no workflow exists yet.
