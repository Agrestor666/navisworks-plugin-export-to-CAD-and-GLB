---
bootstrapped_at: 2026-07-26T10:11:38Z
starter_id: dotnet
starter_name: ".NET (ASP.NET Core webapi)"
project_name: navisworks-export-plugins
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: "dotnet list package --vulnerable"
---

## Hand-off

```yaml
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
```

## Why this stack

Solo build of two Navisworks Manage export plugins (GLB + AutoCAD) in three weeks needs C#/.NET to match the Autodesk host API; JS/TS and other desktop shells (Tauri, Flutter) cannot load inside Navisworks. The registry has no Navisworks plugin starter, so the closest card is `dotnet` (ASP.NET Core webapi) — typed, convention-based, well documented, and bootstrapper-verified for that template. Expect to reshape the scaffold into NW 2023 plugin projects after bootstrap; deploy is local self-host with GitHub Actions on merge. No auth, payments, realtime, AI, or background jobs in MVP scope.

## Pre-scaffold verification

| Signal             | Value                              | Severity | Notes                              |
| ------------------ | ---------------------------------- | -------- | ---------------------------------- |
| npm package        | not run                            | n/a      | language_family is dotnet, not js  |
| GitHub repo        | not run                            | n/a      | docs_url is learn.microsoft.com, not github.com |

Recency: no recency signal available. Proceeding.

## Scaffold log

**Resolved invocation**: `dotnet new webapi -n .bootstrap-scaffold --no-restore`
**Strategy**: subdir-then-move
**Exit code**: 0
**Files moved**: 6
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: absent in scaffold
**.bootstrap-scaffold cleanup**: deleted

Moved items: `.bootstrap-scaffold.csproj`, `.bootstrap-scaffold.http`, `appsettings.json`, `appsettings.Development.json`, `Program.cs`, `Properties/`

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive` (after `dotnet restore`)
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: not distinguished by this tool (no vulnerable packages reported)

Polish CLI message: „Dany projekt «.bootstrap-scaffold» nie ma żadnych pakietów podatnych na zagrożenia, uwzględniając bieżące źródła.”

#### CRITICAL findings

(none)

#### HIGH findings

(none)

#### MODERATE findings

(none)

#### LOW / INFO findings

(none)

## Hints recorded but not acted on

| Hint                       | Value                              |
| -------------------------- | ---------------------------------- |
| bootstrapper_confidence    | verified                           |
| quality_override           | false                              |
| path_taken                 | custom                             |
| self_check_answers         | typed/from_official_starter/conventions/docs_current/can_judge_agent = all true |
| team_size                  | solo                               |
| deployment_target          | self-host                          |
| ci_provider                | github-actions                     |
| ci_default_flow            | auto-deploy-on-merge               |
| has_auth                   | false                              |
| has_payments               | false                              |
| has_realtime               | false                              |
| has_ai                     | false                              |
| has_background_jobs        | false                              |

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- `git init` (if you have not already) to start your own repo history.
- Review any `.scaffold` siblings the conflict policy created and decide which version of each file to keep.
- Address audit findings per your project's risk tolerance — the full breakdown is in this log.
- Reshape this ASP.NET webapi scaffold into Navisworks Manage 2023 plugin projects (GLB + AutoCAD export) — the registry had no Navisworks-specific starter.
