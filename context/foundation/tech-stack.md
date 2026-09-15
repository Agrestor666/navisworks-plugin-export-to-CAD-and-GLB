---
starter_id: dotnet
package_manager: dotnet
project_name: navisworks-export-plugins
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: self-host
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: false
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---

## Why this stack

Solo build of two Navisworks Manage export plugins (GLB + AutoCAD) in three weeks needs C#/.NET to match the Autodesk host API; JS/TS and other desktop shells (Tauri, Flutter) cannot load inside Navisworks. The registry has no Navisworks plugin starter, so the closest card is `dotnet` (ASP.NET Core webapi) — typed, convention-based, well documented, and bootstrapper-verified for that template. Expect to reshape the scaffold into NW plugin projects after bootstrap; deploy is local self-host with GitHub Actions on merge. No auth, payments, realtime, AI, or background jobs in MVP scope.

Living shape: Manage **2026** only — `NavisworksExport.Geometry.2026`, `NavisworksExport.Glb.2026`, `NavisworksExport.AutoCad.2026`. Still `net48`/x64 `AddInPlugin` assemblies, not a return to the webapi scaffold. Manage 2023/2025 are out of scope.
