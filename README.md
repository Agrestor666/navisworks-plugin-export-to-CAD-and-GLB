# Navisworks Export Plugins (GLB & AutoCAD)

Desktop add-ins for **Autodesk Navisworks Manage** that export the **current selection** to formats you can use downstream — interactive 3D in PowerPoint (GLB) or continued design work in AutoCAD (DWG).

Built for BIM coordinators and designers who work with large federated models and need to move **only what they selected**, not the entire project.

---

## Why this exists

Navisworks is excellent for coordination and review, but it does not offer a straightforward way to:

- Drop a **selected subset** of geometry into **PowerPoint 3D** (GLB)
- Continue working on a **selected subset** in **AutoCAD** (DWG)

Common workarounds — screenshots, full-model exports, or multi-step manual pipelines — add friction and lose either interactivity or precision.

These plugins close that gap with a single command from the Add-Ins ribbon.

---

## Plugins

### Export to GLB

Exports the current selection to **glTF Binary (`.glb`)** for **PowerPoint 365 Insert 3D Model**.

| Feature | Detail |
|---|---|
| Scope | Current selection only |
| Colors | Preserved from Navisworks (including appearance overrides) |
| Coordinates | Navisworks Z-up → glTF Y-up |
| Units | Converted to meters (glTF convention) |
| Materials | PBR tuned for PowerPoint (non-metallic, readable shading) |

**Supported hosts:** Navisworks Manage **2023** and **2026**

### Export to AutoCAD

Exports the current selection to **AutoCAD DWG** (AC1032 / AutoCAD 2018+) as colored **PolyfaceMesh** entities.

| Feature | Detail |
|---|---|
| Scope | Current selection only |
| Colors | Linear → sRGB conversion for correct display in AutoCAD |
| Units | Document units → millimeters with `$INSUNITS` header |
| Curved surfaces | Phong tessellation refinement (elbows and bends look smooth, not faceted) |
| Edges | Triangulation seams hidden; real model edges preserved in wireframe |
| Viewport | Defaults to Gouraud shaded on open |

**Supported hosts:** Navisworks Manage **2026**  
*(Manage 2023 AutoCAD export is planned as a reverse-port.)*

---

## Quick start

### Prerequisites

- **Windows** x64
- **.NET Framework 4.8** SDK (for building)
- **Autodesk Navisworks Manage 2023** and/or **2026** (matching the plugin you want)
- **Visual Studio 2022** or `dotnet` CLI (recommended for build)
- For GLB verification: **Microsoft 365 PowerPoint** with 3D model support
- For DWG verification: **AutoCAD 2018+**

### Build

```powershell
dotnet restore
dotnet build NavisworksExportPlugins.sln -c Release
```

Override install paths if Navisworks is not in the default location:

```powershell
dotnet build NavisworksExportPlugins.sln -c Release `
  -p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Manage 2023\" `
  -p:NavisworksInstallDir2026="C:\Program Files\Autodesk\Navisworks Manage 2026\"
```

### Install

Release builds copy plugin assemblies into the Navisworks plugins folder:

| Plugin | Deploy target |
|---|---|
| GLB (2023) | `{NavisworksInstallDir}Plugins\NavisworksExport.Glb\` |
| GLB (2026) | `{NavisworksInstallDir2026}Plugins\NavisworksExport.Glb.2026\` |
| AutoCAD (2026) | `{NavisworksInstallDir2026}Plugins\NavisworksExport.AutoCad.2026\` |

Paths under `Program Files` require an **elevated** terminal or IDE for deploy to succeed. Non-elevated builds still compile; deploy is skipped with a warning.

Restart Navisworks after the first install.

### Use

1. Open a model in Navisworks Manage.
2. Select the objects you want to export.
3. Run **Export to GLB** or **Export to AutoCAD** from the Add-Ins tab.
4. Choose a save location.
5. Open the file in PowerPoint (GLB) or AutoCAD (DWG).

If the selection is empty or has no mesh geometry, the plugin shows an error and does **not** write an empty file.

---

## Architecture

```
NavisworksExportPlugins.sln
├── NavisworksExport.Geometry          Shared COM geometry extraction (2023)
├── NavisworksExport.Geometry.2026     Shared COM geometry extraction (2026)
├── NavisworksExport.Glb               GLB plugin (Manage 2023)
├── NavisworksExport.Glb.2026          GLB plugin (Manage 2026)
├── NavisworksExport.AutoCad           AutoCAD plugin scaffold (Manage 2023)
└── NavisworksExport.AutoCad.2026      AutoCAD DWG plugin (Manage 2026)
```

**Pipeline:** selection → COM `GenerateSimplePrimitives` → world-space triangles → format writer (GLB or DWG).

| Layer | Responsibility |
|---|---|
| `SelectionGeometryExtractor` | Expand selection to geometry leaves, extract triangles via COM, apply transforms and colors |
| `GlbWriter` | SharpGLTF scene build, axis swap, meter scaling |
| `DwgWriter` | ACadSharp PolyfaceMesh output, welding, edge visibility, unit header |
| `CurvedSurfaceRefiner` | Subdivide curved surfaces for flat-shaded AutoCAD rendering |

---

## Tech stack

| Component | Technology |
|---|---|
| Language | C# (.NET Framework 4.8, x64) |
| Host API | Autodesk Navisworks .NET API + COM API |
| GLB writer | [SharpGLTF](https://www.nuget.org/packages/SharpGLTF.Toolkit) 1.0.6 |
| DWG writer | [ACadSharp](https://www.nuget.org/packages/ACadSharp) 3.6.35 |
| UI | WinForms (`SaveFileDialog`, `MessageBox`) |

---

## Design principles

- **Selection only** — never exports the full model.
- **Read-only** — does not modify the Navisworks document.
- **Geometry only** — no BIM properties, IFC metadata, or classification export.
- **Local desktop** — no cloud, auth, or backend.

---

## Debugging

Launch the matching Navisworks host and attach your debugger:

| Host | Executable |
|---|---|
| Manage 2023 | `{NavisworksInstallDir}Roamer.exe` |
| Manage 2026 | `{NavisworksInstallDir2026}Roamer.exe` |

AutoCAD 2026 plugin logs diagnostics to:

```
%TEMP%\NavisworksExport.AutoCad.2026.log
```

Harness projects under `tools/` support offline writer testing without the Navisworks host.

---

## Status

| Capability | Manage 2023 | Manage 2026 |
|---|---|---|
| Export to GLB | Done | Done |
| Export to AutoCAD (DWG) | Planned | Done |

---

## Contributing

This repository is maintained as a focused export tool. Before opening a PR:

1. Read `AGENTS.md` for repository conventions.
2. Keep changes scoped — export **selection only**, read-only on the source model.
3. Test inside the matching Navisworks Manage version before submitting.

Issue reports with reproducible steps (host version, selection type, expected vs actual output) are welcome.

---

## Disclaimer

This project is **not affiliated with, endorsed by, or sponsored by Autodesk**. Navisworks, AutoCAD, and PowerPoint are trademarks of their respective owners.

Use at your own risk. Always verify exported geometry and units in the target application before relying on outputs for production work.
