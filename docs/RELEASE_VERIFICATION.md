# Release verification: v0.3.2

Date: 2026-08-06

This record covers the v0.3.2 source, vendor-free release artifacts, the write-enabled plan-sum dose catalogue entry, and Citrix activation. It does not claim clinical validation of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 143 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- isolated Script Host, its UNC-loading configuration, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.2.exe`
- Citrix binary file version: `0.3.2.0`
- Citrix binary SHA-256: `38311ecb9bc3934534b174cf911eab9dd826be4a0d2005803afdcbb9767d4da6`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.2-win-x64.zip`
- Release ZIP SHA-256: `eeebee9e8036f8c33928bee175c0a6bbc169e4e1fb17fa15f5b135e2d67c6333`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.2.exe`

## Plan-sum tool verification

- `ExportPlanSumDose.esapi.dll` exists at the configured target, has file version `1.1.0.0`, and SHA-256 `cbca2a29c06112cbf0c9b5e556be5cce8a98a2aac50c5d1957dabf0284d709fa`.
- The catalogue entry requires only patient context because plan-sum selection and component review remain inside the plug-in.
- The entry is marked write-enabled and uses `WriteMode=ConfirmSave`; a write-enabled series remains prohibited.
- The plug-in's five rule tests pass, including additive and mixed fractionation proposals, missing prescriptions, and bounded unique plan IDs.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. It contains the enabled `export-plansum-dose-direct` entry, resolves the productive binary below `ESAPI-MG\plugins`, and links InHouse script ID 65. Existing application paths and arguments were otherwise left unchanged.

## Clinical boundary

The release verifies launcher behavior and synthetic context handling. Each configured read-only or write-enabled application still requires its own Eclipse authorization, save confirmation where applicable, clinical review, and local validation before clinical use.
