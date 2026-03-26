# .github KNOWLEDGE BASE

Use this folder for process and automation changes only.

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| CI build and artifacts | `.github/workflows/dotnet.yml` | build/test pipeline, WSearch service, artifact publishing |
| Release deploy | `.github/workflows/release_deploy.yml` | website + Chocolatey dispatches, secret-driven |
| Release PR body automation | `.github/workflows/release_pr.yml`, `.github/update_release_pr.py` | expects one open release PR and milestone grouping |
| Built-in plugin publishing | `.github/workflows/default_plugins.yml` | publishes plugin repos, updates plugin metadata |
| Issue/PR automation | `.github/workflows/pr_*.yml`, `stale.yml`, `spelling.yml` | author assignment, milestone, stale, spelling |
| Dependency policy | `.github/dependabot.yml` | daily cadence, PR caps, ignored packages |
| Intake rules | `.github/ISSUE_TEMPLATE/` | bug report required fields, feature request, code review template |

## LOCAL RULES
- Keep workflow guidance here; do not duplicate build/test basics from root.
- `release_pr` automation depends on milestone-driven grouping and exactly one open PR labeled `release`.
- `default_plugins.yml` is not generic CI; it couples plugin publishing to repo/version metadata.
- `release_deploy.yml` has external side effects and secret requirements; treat edits as release-engineering work.
- `dotnet.yml` includes Windows-service setup (`WSearch`) and artifact behavior worth preserving.

## ANTI-PATTERNS
- Do not move workflow-specific secrets/process rules into root `AGENTS.md`.
- Do not treat `.github/workflows/` as its own child boundary unless process complexity grows further.
- Do not edit release automation without reading the paired workflow/script/template files together.

## UNIQUE FILES
- `appveyor.yml` at repo root is still part of release/version behavior; check it alongside `.github` workflows.
- `.github/ISSUE_TEMPLATE/discussion.md.not_used` is legacy/disused; avoid relying on it as active process.
