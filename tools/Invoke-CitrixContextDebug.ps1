[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$ApplicationId,
    [Parameter(Mandatory = $true)][string]$PatientId,
    [string]$CourseId,
    [string]$PlanId,
    [string]$PlanSumId,
    [string]$StructureSetId,
    [string]$ImageId,
    [string[]]$PlanIdsInScope,
    [string[]]$PlanSumIdsInScope,
    [int]$WaitSeconds = 180,
    [string]$RequestDirectory = '\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\Logs\ESAPI-Runner-Hub\requests'
)

$ErrorActionPreference = 'Stop'
if ($WaitSeconds -lt 1 -or $WaitSeconds -gt 1800) { throw 'WaitSeconds must be between 1 and 1800.' }

$requestId = 'debug-{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 10))
$requestPath = Join-Path $RequestDirectory ($requestId + '.request.json')
$resultPath = Join-Path $RequestDirectory ($requestId + '.result.json')
$pendingPath = Join-Path $RequestDirectory ($env:USERNAME + '.pending')
New-Item -ItemType Directory -Path $RequestDirectory -Force | Out-Null
if (Test-Path -LiteralPath $pendingPath) { throw "Another Citrix context request is pending for $env:USERNAME: $pendingPath" }

$context = [ordered]@{ PatientId = $PatientId }
if ($CourseId) { $context.CourseId = $CourseId }
if ($PlanId) { $context.PlanId = $PlanId }
if ($PlanSumId) { $context.PlanSumId = $PlanSumId }
if ($StructureSetId) { $context.StructureSetId = $StructureSetId }
if ($ImageId) { $context.ImageId = $ImageId }
if ($PlanIdsInScope) { $context.PlanIdsInScope = @($PlanIdsInScope) }
if ($PlanSumIdsInScope) { $context.PlanSumIdsInScope = @($PlanSumIdsInScope) }

$request = [ordered]@{
    ApplicationId = $ApplicationId
    Contexts = @($context)
}
$request | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestPath -Encoding UTF8

$pendingTemporary = $pendingPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$requestId | Set-Content -LiteralPath $pendingTemporary -Encoding ASCII
Move-Item -LiteralPath $pendingTemporary -Destination $pendingPath

$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ESAPI-Runner-Hub.lnk'
if (-not (Test-Path -LiteralPath $shortcut -PathType Leaf)) { throw "Citrix ESAPI Runner Hub shortcut not found: $shortcut" }
Start-Process -FilePath $shortcut | Out-Null

$deadline = (Get-Date).AddSeconds($WaitSeconds)
while (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    if ((Get-Date) -ge $deadline) {
        throw "Citrix context request timed out after $WaitSeconds seconds. Request: $requestPath"
    }
    Start-Sleep -Seconds 1
}

$result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
[pscustomobject]@{
    RequestId = $requestId
    RequestPath = $requestPath
    ResultPath = $resultPath
    ApplicationId = $result.ApplicationId
    Vda = $result.ComputerName
    Status = $result.Status
    ExitCode = [int]$result.ExitCode
    StartedUtc = $result.StartedUtc
    FinishedUtc = $result.FinishedUtc
    ExceptionType = $result.ExceptionType
}

exit [int]$result.ExitCode
