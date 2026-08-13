# Context and Citrix debugging

The Runner Hub exposes a deterministic command-oriented interface for configured ESAPI context scripts. It is intended for repeatable troubleshooting and test automation, not as a general remote shell.

The deployment needs only one Citrix Published Application for the Hub. Exact automated tests use the user-scoped shared request below, so the workflow does not depend on client argument forwarding or a separately published application for every script.

## Mental model

There are two separate transport steps:

1. A workstation writes an exact request into the configured protected request directory.
2. The ordinary published Citrix application starts without extra client arguments. On the assigned VDA, the Runner claims only the pending request for the current Windows SID, verifies the same SID in the request, executes it, and writes a result file.

This design avoids relying on Citrix Workspace to forward a command line and avoids VDA-local `latest` history. Patient and planning identifiers are present only in the explicitly protected request file and the private Runner-to-Host context payload; technical logs do not contain them.

## Recommended workstation command

```powershell
.\tools\Invoke-CitrixContextDebug.ps1 `
  -ApplicationId plugin-color-code `
  -PatientId PATIENT-ID `
  -CourseId COURSE-ID `
  -PlanId PLAN-ID
```

Optional parameters are `PlanSumId`, `StructureSetId`, `ImageId`, `PlanIdsInScope`, `PlanSumIdsInScope`, `ClaimSeconds`, `WaitSeconds`, and `RequestDirectory`.

The helper:

1. validates the application ID and timeout;
2. writes `<request-id>.request.json`;
3. atomically writes `<windows-SID>.pending` containing only the request ID;
4. opens the installed `ESAPI-Runner-Hub` Citrix shortcut without arguments;
5. waits for `<request-id>.result.json` and returns its fields as a PowerShell object.

Only one request may be pending per Windows SID. The marker can be claimed for at most 30 seconds by default and is normally moved atomically within seconds. Opening the Hub as another user cannot start it. The request and result remain as readable evidence in the protected request directory; pending and claimed markers are short-lived and are removed after processing or timeout.

## Request contract

```json
{
  "RequestedBySid": "S-1-5-21-...",
  "RequestedBy": "DOMAIN\\user",
  "ApplicationId": "plugin-color-code",
  "Contexts": [
    {
      "PatientId": "PATIENT-ID",
      "CourseId": "COURSE-ID",
      "PlanId": "PLAN-ID",
      "PlanSumId": null,
      "StructureSetId": null,
      "ImageId": null,
      "PlanIdsInScope": ["PLAN-ID"],
      "PlanSumIdsInScope": []
    }
  ]
}
```

JSON may be UTF-8 with or without a byte-order mark. `RequestedBySid` is mandatory and must equal the Windows SID executing the Runner; a mismatch is refused without writing a result, so the rightful user can still process the request. Request IDs must match the Runner's restricted filename-safe format. A request with more than 100 contexts is rejected. More than one context is accepted only for applications configured as read-only.

## Result contract

```json
{
  "RequestId": "debug-YYYYMMDD-HHMMSS-token",
  "ApplicationId": "plugin-color-code",
  "ComputerName": "VDA-NAME",
  "ExitCode": 0,
  "Status": "completed",
  "StartedUtc": "...",
  "FinishedUtc": "..."
}
```

`Status=completed` and `ExitCode=0` mean that the configured Script Host returned successfully. A failed result records a non-zero exit code and, when available, only the exception type. Script output and clinical identifiers are not copied into technical logs.

## Direct commands inside a VDA session

If a shell is already open inside the assigned VDA, an existing request owned by the same Windows SID can be executed directly:

```powershell
ESAPI-Runner-Hub.exe --run-request REQUEST-ID --settings .\settings.ini
```

An explicit single context can instead be supplied through private process environment variables:

```powershell
$env:ESAPI_RUNNER_CONTEXT_PATIENT = 'PATIENT-ID'
$env:ESAPI_RUNNER_CONTEXT_COURSE = 'COURSE-ID'
$env:ESAPI_RUNNER_CONTEXT_PLAN = 'PLAN-ID'
ESAPI-Runner-Hub.exe --run-context APPLICATION-ID --settings .\settings.ini
Remove-Item Env:ESAPI_RUNNER_CONTEXT_PATIENT, Env:ESAPI_RUNNER_CONTEXT_COURSE, Env:ESAPI_RUNNER_CONTEXT_PLAN
```

For a read-only series, provide `ESAPI_RUNNER_CONTEXTS` and use `--run-contexts APPLICATION-ID`. The Runner starts one isolated Script Host at a time and stops at the first non-zero exit.

These commands must run inside the VDA. Starting the shared executable directly on a workstation does not move the process into Citrix and cannot provide the VDA's ESAPI runtime.

## Automation procedure for humans or agents

1. Read the live `settings.ini`; resolve `LogDirectory`, `ContextRequestDirectory`, the application ID, context requirement, scope mode, and write mode. Keep the request directory below the protected readable log tree (the default is `%LOCALAPPDATA%\ESAPI Runner Hub\Logs\requests`).
2. Verify that the selected application is enabled and that its configured target exists.
3. Use the helper whenever the caller is outside the VDA. Do not construct `wfcrun32`, `qlaunch`, or nested `cmd.exe` quoting to transport clinical context.
4. Wait for the matching result file, not for a guessed delay or the presence of a Citrix window.
5. Treat the result exit code as the process outcome and correlate it with the per-VDA launcher log plus the per-process Runner/Script Host log.
6. Preserve request and result files when they are diagnostic evidence. Never copy their identifiers into public issue reports or ordinary technical logs.
7. For write-enabled tools, send exactly one context and keep the interactive save/discard decision. Do not automate confirmation of clinical modifications.

## Common failures

- No result and a remaining `.pending` marker: the normal published application did not claim it for that Windows SID; the helper removes its own marker after the claim timeout.
- No result and a `.claimed-*` marker: the VDA claimed the job but terminated before cleanup; inspect launcher and Runner logs.
- `context_request_owner_mismatch`: another Windows identity attempted the request; no result is written and the request remains available to its owner.
- Result with exit code `2`: invalid request, unavailable application, unsupported context mode, or configuration failure.
- Result with the Script Host's non-zero code: context resolution or the target script failed; inspect the safe Script Host phase and exception type.
- `wfcrun32` error before a launcher `START` entry: failure occurred in the Citrix client path, not in the Runner.
- `--replay-latest` finds nothing or the wrong VDA: history is DPAPI-protected and local to the Windows account on that VDA; use an exact shared request instead.
