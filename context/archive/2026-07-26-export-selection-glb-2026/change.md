---
change_id: export-selection-glb-2026
title: Export selection to GLB on Navisworks Manage 2026 (PowerPoint 3D)
status: archived
created: 2026-07-26
updated: 2026-07-26
archived_at: 2026-07-26T18:40:00Z
---

## Notes

Roadmap S-03. Ports the proven S-01 GLB export flow (`export-selection-glb`) onto the Manage 2026 twin host scaffolded by F-02 (`nw-plugin-scaffold-2026`). Confirmed via direct build test that `NavisworksExport.Geometry` and the full `NavisworksExport.Glb` chain compile unchanged against Manage 2026's `Autodesk.Navisworks.Api`/`ComApi`/`Interop.ComApi` DLLs — no host API breaking changes — so the plan shares source via linked files (`NavisworksExport.Geometry.2026` twin project + linked `GlbWriter.cs`) rather than duplicating the COM/dedup/axis-conversion logic.
