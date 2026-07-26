---
change_id: export-selection-glb
title: Export selection to GLB (PowerPoint 3D)
status: implementing
created: 2026-07-26
updated: 2026-07-26
archived_at: null
---

## Notes

Roadmap S-01 (north star). Selection → GLB → interactive 3D in PowerPoint, on top of the `nw-plugin-scaffold` (F-01) host. Introduces the shared `NavisworksExport.Geometry` COM-extraction library (also needed by the future `export-selection-autocad` slice per its research doc), a SharpGLTF-based writer with Navisworks→glTF unit/axis conversion, and the real command wiring (FR-007/FR-008) on top of the existing `GlbExportCommand` stub.

Phase 2 (GLB writer) landed in `b6d068e`.
