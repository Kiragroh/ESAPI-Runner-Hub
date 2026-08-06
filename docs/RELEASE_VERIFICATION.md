# Release verification: v0.3.1

Date: 2026-08-06

This record covers the v0.3.1 source, vendor-free release artifacts, direct export-tool configuration, and Citrix activation. It does not claim clinical validation of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 142 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- isolated Script Host, its UNC-loading configuration, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.1.exe`
- Citrix binary file version: `0.3.1.0`
- Citrix binary SHA-256: `b0efe0335418e2a11a3e4ee824381dbbc9ba51e6253a69b5d0fc78d4bbc743fc`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.1-win-x64.zip`
- Release ZIP SHA-256: `1bcc7d2c2e5518aa80ccf76d5f43f279910f660e7e4d6b337e0cc257ac9f8bf2`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.1.exe`

## Export-tool verification

- `GetDicomCollectionUKL.cs` compiled successfully through the released source compiler against the Eclipse 18 references; the cached assembly size was 34,304 bytes.
- `ExportPlansQuicker.esapi.dll` exists at the configured UNC target, is 134,656 bytes, and has SHA-256 `f3f04bfd876c2e8244b5b9c0499a24dec572acb568ef78fe4f24462a1b868194`.
- Both catalogue entries require a patient and retain plan selection inside the target exporter.
- The Script Host release includes `loadFromRemoteSources` so the configured alternate UNC binary can be loaded by .NET Framework.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. Its STR Hub base URL now uses `http://10.100.86.9:5173/`; the README action copies the resulting per-tool URL to the clipboard and does not start a browser. Two enabled read-only patient-context entries were added for the requested source and binary exporters. Existing application paths and arguments were otherwise left unchanged.

## Clinical boundary

The release verifies launcher behavior and synthetic context handling. Each configured read-only or write-enabled application still requires its own Eclipse authorization, save confirmation where applicable, clinical review, and local validation before clinical use.
