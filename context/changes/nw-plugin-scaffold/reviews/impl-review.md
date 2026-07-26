<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Navisworks Plugin Host Scaffold

- **Plan**: context/changes/nw-plugin-scaffold/plan.md
- **Scope**: Phases 1–4 of 4
- **Date**: 2026-07-26
- **Verdict**: NEEDS ATTENTION → triaged (all findings fixed)
- **Findings**: 0 critical  3 warnings  1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Intentional adaptations not recorded in the plan

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: Directory.Build.props; both .csproj; *ExportCommand.cs
- **Detail**: Three adaptations diverge from written contracts (NuGet fallback, LangVersion=latest, NwApplication alias).
- **Fix A ⭐ Recommended**: Add a short plan addendum listing the three adaptations
- **Decision**: FIXED via Fix A

### F2 — Deploy RemoveDir-then-Copy can wipe a working plugin

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: NavisworksExport.Glb/...csproj:33-35 (same in AutoCad)
- **Detail**: RemoveDir before Copy with ContinueOnError can delete a working plugin if copy fails.
- **Fix A ⭐ Recommended**: Drop RemoveDir; Copy with overwrite only
- **Decision**: FIXED via Fix A

### F3 — NavisworksInstallDir env override lacks trailing-slash normalize

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Directory.Build.props:3
- **Detail**: Env override without trailing separator breaks HintPath/deploy concatenation.
- **Fix**: Normalize with MSBuild EnsureTrailingSlash
- **Decision**: FIXED

### F4 — Solution x64/x86 configs map to Any CPU

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: NavisworksExportPlugins.sln:22-43
- **Detail**: Solution x64 built Any CPU; x64 relied on PlatformTarget alone.
- **Fix**: Platforms=x64 in Directory.Build.props; sln maps all configs to project x64
- **Decision**: FIXED
