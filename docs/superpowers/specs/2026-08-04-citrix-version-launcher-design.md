# Citrix version launcher design

Date: 2026-08-04

## Problem and objective

Citrix Studio currently references `dist\ESAPI-Runner-Hub.exe`. A failed replacement left that path as an inaccessible zero-byte file. A published or otherwise server-held executable can also remain locked while a session exists, which makes in-place releases fragile.

The objective is to give Citrix Studio one durable command that does not change between Hub releases. Each Hub release is stored under a new versioned filename, and a small pointer selects the version used for new launches. Existing sessions may continue using an older binary without blocking deployment or rollback.

## Chosen approach

Citrix Studio publishes the Windows command processor with the stable batch launcher as its argument. The launcher reads a filename from `citrix\current.txt`, validates it, and starts the corresponding binary from `dist\versions`. It waits for that process so Citrix can track the published application lifecycle.

The Hub remains the visible application chooser. The launcher chooses only the Hub release. `current.txt` therefore contains a filename, not arbitrary command text.

This approach is preferred over a custom bootstrapper executable because the stable batch file does not require its own build or release lifecycle. It is preferred over changing the Studio application path for every release because activation and rollback become a one-line pointer change.

## Files and locations

The repository gains these files:

- `citrix\Start-ESAPI-Runner-Hub.cmd`: stable Citrix entry point.
- `citrix\current.txt`: active versioned Hub filename.
- `citrix\README-Citrix.md`: exact Studio configuration, release switch, rollback, and troubleshooting instructions.
- `tests\Test-CitrixLauncher.ps1`: isolated launcher behavior tests using a temporary directory and the existing synthetic runner fixture.

Release binaries are stored as:

```text
dist\versions\ESAPI-Runner-Hub.v0.1.2.exe
dist\versions\ESAPI-Runner-Hub.vNEXT.exe
```

The existing live `dist\settings.ini` remains the only live Hub configuration. The launcher passes its absolute path with `--settings`, so moving the binary into `dist\versions` does not create a second configuration.

## Citrix Studio command

Citrix Studio keeps a stable local executable path:

```text
C:\Windows\System32\cmd.exe
```

Arguments:

```text
/d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd""
```

The working directory is the repository's `citrix` directory. The launcher uses its own location as the source of truth, so drive mappings and the Studio server's current working directory are irrelevant.

## Validation and security behavior

The launcher performs these checks before starting anything:

1. `current.txt` exists and contains exactly one non-empty filename.
2. The value has no directory component and ends in `.exe`.
3. The resolved target exists under `dist\versions`.
4. The shared `dist\settings.ini` exists.

It never evaluates command text from the pointer and never searches arbitrary directories. Hub arguments supplied by Citrix are forwarded, but neither arguments nor patient identifiers are written to the launcher log.

## Process and error handling

The launcher starts the selected Hub with the versioned binary's directory as working directory, passes the shared settings path, waits for completion, and returns the Hub exit code. This keeps child crashes isolated according to the Hub's existing behavior while retaining normal Citrix process tracking.

Configuration errors produce a short visible German error message and a non-zero exit code. A compact technical log is written below `%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs`. Network logging remains inside the Hub's existing bounded logging implementation; the batch launcher does not probe or write an optional network log before startup, avoiding another network-path freeze path. Logs contain timestamps, event categories, selected release filenames, and exit codes only.

## Release activation and rollback

A release is deployed in this order:

1. Build and test the new release locally.
2. Copy it to `dist\versions` under a new immutable versioned filename.
3. Verify size, version metadata, and SHA-256 at the final UNC path.
4. Atomically replace `citrix\current.txt` with the new filename.
5. Smoke-launch through the stable batch entry point.

Rollback changes only `current.txt` to a previously verified filename. Older binaries are retained while they might still be running and may be removed later after confirming that no handle exists.

The release script must never overwrite or remove `dist\settings.ini` and must not overwrite an existing versioned binary with different content.

## Tests and acceptance criteria

The automated launcher test must first fail before the launcher exists, then cover:

- successful selection and launch of a versioned synthetic executable;
- forwarding of a harmless synthetic argument without logging its value;
- rejection of an empty pointer;
- rejection of a path or non-EXE pointer;
- a clear failure for a missing target or missing settings file;
- propagation of a non-zero child exit code;
- creation of a privacy-safe local technical log.

Repository tests and the release build must remain green. Final operational acceptance requires:

- the selected versioned Hub binary has the expected hash and file version on the share;
- the launcher starts the Hub from the UNC path in `--offline-ui-smoke` mode;
- changing `current.txt` switches subsequent launches without modifying the Citrix Studio application;
- the live `settings.ini` hash is unchanged;
- no Varian assemblies are included in the release.

This is a deployment and process-isolation change. It does not alter ESAPI patient access, patient lifetime, clinical checks, or write permissions.
