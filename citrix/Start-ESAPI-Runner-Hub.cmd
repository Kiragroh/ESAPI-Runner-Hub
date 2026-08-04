@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "LAUNCHER_DIR=%~dp0"
for %%D in ("%LAUNCHER_DIR%..") do set "ROOT=%%~fD"
set "POINTER=%LAUNCHER_DIR%current.txt"
set "VERSIONS=%ROOT%\dist\versions"
set "SETTINGS=%ROOT%\dist\settings.ini"
set "LOGDIR=%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs"
if not exist "%LOGDIR%" md "%LOGDIR%" >nul 2>&1
set "LOGFILE=%LOGDIR%\CitrixLauncher.log"

if not exist "%POINTER%" goto PointerMissing

set "TARGET_FILE="
set "POINTER_EXTRA="
for /f "usebackq delims=" %%I in ("%POINTER%") do (
  if defined TARGET_FILE (set "POINTER_EXTRA=1") else set "TARGET_FILE=%%I"
)

if not defined TARGET_FILE goto PointerInvalid
if defined POINTER_EXTRA goto PointerInvalid
%SystemRoot%\System32\findstr.exe /R /X /I /C:"ESAPI-Runner-Hub\.v[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\.exe" "%POINTER%" >nul
if errorlevel 1 goto PointerInvalid

set "TARGET=%VERSIONS%\%TARGET_FILE%"
if not exist "%TARGET%" goto TargetMissing
if not exist "%SETTINGS%" goto SettingsMissing

call :Log START release=%TARGET_FILE%
pushd "%VERSIONS%" || goto WorkingDirectoryFailed
start "" /wait "%TARGET%" --settings "%SETTINGS%" %*
set "CHILD_EXIT=%ERRORLEVEL%"
popd
call :Log EXIT release=%TARGET_FILE% code=%CHILD_EXIT%
exit /b %CHILD_EXIT%

:PointerMissing
call :Log ERROR code=20 reason=pointer_missing
>&2 echo ESAPI Runner Hub: Die Versionsauswahl current.txt fehlt.
exit /b 20

:PointerInvalid
call :Log ERROR code=21 reason=pointer_invalid
>&2 echo ESAPI Runner Hub: current.txt enthaelt keinen gueltigen Versions-Dateinamen.
exit /b 21

:TargetMissing
call :Log ERROR code=22 reason=target_missing
>&2 echo ESAPI Runner Hub: Die ausgewaehlte Programmversion wurde nicht gefunden.
exit /b 22

:SettingsMissing
call :Log ERROR code=23 reason=settings_missing
>&2 echo ESAPI Runner Hub: Die gemeinsame settings.ini wurde nicht gefunden.
exit /b 23

:WorkingDirectoryFailed
call :Log ERROR code=24 reason=working_directory
>&2 echo ESAPI Runner Hub: Das Versionsverzeichnis ist nicht erreichbar.
exit /b 24

:Log
>>"%LOGFILE%" echo [%date% %time%] %*
exit /b 0
