# Release verification: v0.1.0

Date: 2026-08-01

This record documents the release gates for ESAPI Runner Hub v0.1.0. The automated checks use synthetic data only; live Eclipse validation remains a separate workstation gate.

## Automated release gates

- Release build: x64, .NET Framework 4.8, single executable.
- Test suite: 26 automated tests, including configuration parsing, detached patient search, reflection-only ESAPI loading, launch contracts, child-process isolation, privacy-safe diagnostics, and release metadata.
- Vendor-free package: no `VMS.TPS.*`, Varian, or EsapiEssentials binaries in the public repository or ZIP.
- Packaging: deterministic ZIP generation with package-internal and release-level SHA-256 manifests.
- Offline UI smoke mode: synthetic patients and applications, no ESAPI access.
- Branding: the project icon is embedded as the executable, taskbar, and window icon and is also displayed in the application header.

Authoritative command:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

Final ZIP SHA-256: `cd1384f50e02c9d24d5b9607fab0e3429120ccaa920ebae57b7c3cbffe0d2d0f`. The same value is recorded in `dist/SHA256SUMS.txt` and on the GitHub release.

## Internal deployment gates

- Shared checkout: `\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub`
- Portable executable: `dist\ESAPI-Runner-Hub.exe`
- Local configuration: `dist\settings.ini` (ignored by Git and preserved by the release build)
- Configured applications: ClearPlan / PlanCheck, eDoc Uploader, and Eclipse Data Miner
- Configured ESAPI assemblies: local Eclipse 16.1 API and Types paths
- Path check: all three configured application executables existed at verification time
- STR-Hub InHouse record: ID 62, category `Eclipse`, version source `git`
- Visible STR-Hub check: the authenticated InHouse view displayed `ESAPI Runner Hub` as version `v0.1.0.1` in the Eclipse category
- InHouse database backup before insertion: `inhouse_backup_before_add_esapi_runner_hub_20260801_140350.db`

## Clinical boundary

The current workstation does not provide the Eclipse/ESAPI runtime. Therefore, the following are not claimed by this release record:

- successful connection to the production Eclipse database;
- authorization for a specific clinical user role;
- compatibility of every configured child application with concurrent ESAPI use;
- clinical validation of any child application.

Run the separate [clinical validation checklist](CLINICAL_VALIDATION.md) on a designated Eclipse workstation before clinical use.
