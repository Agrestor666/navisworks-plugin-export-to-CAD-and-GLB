# Export Selection to GLB on Navisworks Manage 2026 — Plan Brief

> Full plan: `context/changes/export-selection-glb-2026/plan.md`

## What & Why

Port the proven "Export to GLB" flow (S-01) onto the Manage **2026** twin host (F-02) — replacing its selection-count stub with the real COM-extraction → glTF-writing → save-dialog flow — so the product's north-star capability (selection → GLB → interactive 3D in PowerPoint) works on both supported hosts, not just Manage 2023.

## Starting Point

`NavisworksExport.Glb.2026` is a loadable stub from F-02 (reports selection count only). The real implementation already exists and is fully proven on Manage 2023: `NavisworksExport.Geometry` (COM extraction + fragment dedup) → `GlbWriter` (SharpGLTF, Z-up→Y-up conversion) → `GlbExportCommand` (errors, save dialog). Neither is wired to the 2026 project, which also lacks the COM assembly references (`ComApi`, `Interop.ComApi`) and `SharpGLTF.Toolkit` package that extraction/writing need.

## Desired End State

Clicking "Export to GLB" inside Navisworks Manage 2026 behaves identically to the 2023 plugin: a real selection exports to `.glb` and opens correctly in PowerPoint; empty/zero-triangle selections show the same clear errors; the source document is never touched; the 2023 plugin is completely unaffected.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Code-sharing architecture | Linked source files (twin projects) | Empirically confirmed via a direct build test that `NavisworksExport.Geometry` and the full GLB export chain compile unchanged against Manage 2026's host DLLs — sharing the tricky COM/dedup/axis-conversion source avoids silent drift between two copies. |
| API compatibility risk | Resolved via direct test, not assumption | Rather than asking "what might have changed," both hosts were actually installed and the exact source was test-compiled against 2026's `Api`/`ComApi`/`Interop.ComApi` DLLs — zero errors, zero changes needed. |
| 2026 UX text | Identical to 2023 | Only the pre-existing distinct `Plugin` id (`GlbExport2026`) differs; all dialogs/messages match 2023 verbatim — same capability, new host. |
| Host verification depth | Full repeat of S-01's test matrix | Identical source compiling cleanly doesn't guarantee identical COM-marshaling/performance behavior at runtime on the new host — this is the first live run of this artifact on Manage 2026. |
| GlbWriterHarness reuse | Skip | `GlbWriter.cs` is a linked, host-API-free file already sanity-checked in S-01; re-running the hardcoded-triangle harness against it would be redundant. |
| Roadmap hygiene | Fix F-02's stale status in this plan | `roadmap.md` is untracked in git and not touched by the `/10x-implement` epilogue convention — nothing else will fix it; cheap to do while touching the file for S-03. |

## Scope

**In scope:** new `NavisworksExport.Geometry.2026` twin project (linked source, 2026 COM refs); `SharpGLTF.Toolkit` + `Geometry.2026` references and a fixed multi-DLL deploy target on `NavisworksExport.Glb.2026`; linked `GlbWriter.cs`; real `GlbExportCommand.cs` on the 2026 twin; full live verification in Manage 2026 + PowerPoint; `roadmap.md` status fix for F-02 and S-03.

**Out of scope:** `NavisworksExport.AutoCad.2026` (S-04, still blocked on S-02); any change to the 2023 `NavisworksExport.Glb`/`NavisworksExport.Geometry` projects; 2026-specific UX differences; `NavisworksAPIdlls2026` NuGet fallback; PRD/AGENTS.md changes (already accurate); re-running the GlbWriterHarness check.

## Architecture / Approach

`NavisworksExport.Geometry.2026` links the same four `.cs` files as `NavisworksExport.Geometry` but compiles them against Manage 2026's COM assemblies (`HintPath` from `NavisworksInstallDir2026`, install-required, no NuGet fallback — matching F-02's convention). `NavisworksExport.Glb.2026` links `GlbWriter.cs` from the 2023 project and adds a `ProjectReference` to `Geometry.2026` plus the same `SharpGLTF.Toolkit` package — mirroring 2023's `NavisworksExport.Glb` structure exactly, just pointed at the 2026 twin of each shared piece. Only `GlbExportCommand.cs` is real, non-linked content on each side, since it already carries a host-distinct `Plugin` id from F-02.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Shared geometry-extraction twin | `NavisworksExport.Geometry.2026` (linked source, 2026 COM refs) in the `.sln` | Solution config-platform mapping mistakes silently break `dotnet build` for the new project |
| 2. GLB writer wiring | Linked `GlbWriter.cs` + SharpGLTF/Geometry.2026 refs + fixed multi-DLL deploy target | Forgetting the deploy-target fix means the plugin builds but fails to load at runtime (missing dependency DLLs) |
| 3. Command wiring | Real `GlbExportCommand.cs` on the 2026 twin, matching 2023 behavior/text | Accidentally diverging message text or the Plugin id from the confirmed "identical UX" decision |
| 4. Host verification & roadmap fix | Full live Manage 2026 + PowerPoint test matrix; `roadmap.md` F-02/S-03 status corrected | Non-elevated deploy looks "green" but the plugin (or its dependencies) never actually lands in Plugins\ |

**Prerequisites:** F-02 (`nw-plugin-scaffold-2026`) done — confirmed via `git log`; Navisworks Manage 2026 installed with a real model; PowerPoint available; elevated terminal/IDE for deploy.
**Estimated effort:** ~1-2 sessions across 4 phases — smaller than S-01 since the hardest logic (COM extraction, axis conversion) is reused, not rebuilt.

## Open Risks & Assumptions

- Compile-time API compatibility (verified) does not guarantee runtime COM-marshaling behaves identically on Manage 2026 — this is exactly why Phase 4 repeats S-01's full test matrix rather than a lighter pass.
- Assumes the machine used for Phase 4 verification has both Manage 2026 and PowerPoint available, matching what was available during this planning session.
- Assumes linking `GlbWriter.cs`'s `namespace NavisworksExport.Glb` into an assembly whose `RootNamespace` is `NavisworksExport.Glb2026` causes no build/runtime issue — C# namespaces are independent of assembly `RootNamespace`, but this is worth a quick visual check in Phase 2's build output before Phase 3 depends on it.

## Success Criteria (Summary)

- A real Navisworks Manage 2026 selection exports to `.glb` and opens in PowerPoint as an interactive, correctly oriented, colored 3D model — matching 2023 behavior exactly.
- Empty selections and selections with no extractable geometry show the same clear error as 2023, on the 2026 host.
- The source Navisworks Manage 2026 document is unchanged after export, and the 2023 plugin remains fully unaffected.
