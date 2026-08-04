# Release verification: v0.3.0

Date: 2026-08-04

This record covers the v0.3.0 source, vendor-free release artifacts, synthetic UI inspection, and Citrix activation. It does not claim clinical validation of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 139 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- isolated Script Host and context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.0.exe`
- Citrix binary file version: `0.3.0.0`
- Citrix binary SHA-256: `0cba382f090610af324c4159182d0b15fd164ec04f41dd4f7c323f601b36ed16`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.0-win-x64.zip`
- Release ZIP SHA-256: `515d5d7b6e70dc6ba143201d1d82284b0023f6e5484b890a9db7204cf150bc53`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.0.exe`

## Synthetic UI inspection

The final offline smoke build was inspected at 1586 x 893 pixels. It displayed four cards in one row, no horizontal catalogue scrollbar, a replayable completed activity, a selected synthetic plan, and the centered filter bar. Privacy mode was then enabled and visibly obscured patient/context/path fields while replacing patient-specific launch labels with generic text. The resulting public screenshot is `docs/images/esapi-runner-hub-overview.png`.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. Before and after the English catalogue-copy update, all lines except `Name`, `Category`, and `Description` were byte-equivalent after normalization: 452 lines, SHA-256 `2027f0cf9531597585903c7a021c52af735b5346d01256e93e38c1bf0b51b021`. Paths, arguments, access modes, context rules, ESAPI assemblies, and logging locations were not changed.

## Clinical boundary

The release verifies launcher behavior and synthetic context handling. Each configured read-only or write-enabled application still requires its own Eclipse authorization, save confirmation where applicable, clinical review, and local validation before clinical use.
