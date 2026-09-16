[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunnerAssembly)

# Synthetic offscreen lifecycle fixture. Never shows a Window, loads settings,
# queries ESAPI, launches a tool, or touches the current user's history.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$runner=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($RunnerAssembly))
# Resolve the compiled Window icon/resources against Runner, not powershell.exe.
[Windows.Application]::ResourceAssembly=$runner
$fixtureApplication=[Windows.Application]::new()
$fixtureApplication.ShutdownMode='OnExplicitShutdown'
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$previousContext=[Threading.SynchronizationContext]::Current
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new())

function New-FixtureViewModel {
    return [EsapiRunnerHub.ViewModels.MainViewModel]::new(
        [EsapiRunnerHub.Configuration.HubConfiguration]::new(),
        [EsapiRunnerHub.Patients.PatientRecord[]]@(), $null,
        [EsapiRunnerHub.History.ProtectedContextEnvelope]::new())
}

function Wait-FixtureClose($window, [int]$maximumMilliseconds) {
    $frame=[Windows.Threading.DispatcherFrame]::new()
    $timer=[Windows.Threading.DispatcherTimer]::new()
    $timer.Interval=[TimeSpan]::FromMilliseconds(10)
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $tick={
        if ($window.GetType().GetField('closeAfterHistoryFlush',$flags).GetValue($window) -or $watch.ElapsedMilliseconds -gt $maximumMilliseconds) {
            $frame.Continue=$false
        }
    }.GetNewClosure()
    $timer.Add_Tick($tick)
    try { $timer.Start(); [Windows.Threading.Dispatcher]::PushFrame($frame) }
    finally { $timer.Stop(); $timer.Remove_Tick($tick) }
    if (-not $window.GetType().GetField('closeAfterHistoryFlush',$flags).GetValue($window)) { throw 'Close drain did not complete.' }
    return $watch.ElapsedMilliseconds
}

try {
    $window=[EsapiRunnerHub.MainWindow]::new()
    $old=New-FixtureViewModel
    $new=New-FixtureViewModel
    $owners=$window.GetType().GetField('historyOwners',$flags).GetValue($window)
    $owners.Add($old); $owners.Add($new)
    $window.DataContext=$new
    $local=[Threading.Tasks.TaskCompletionSource[bool]]::new()
    $shared=[Threading.Tasks.TaskCompletionSource[bool]]::new()
    $old.GetType().GetField('localHistoryWrites',$flags).SetValue($old,$local.Task)
    $old.GetType().GetField('historySynchronization',$flags).SetValue($old,$shared.Task)
    $closing=$window.GetType().GetMethod('WindowClosing',$flags)
    $event=[ComponentModel.CancelEventArgs]::new()
    [void]$closing.Invoke($window,@($window,$event))
    if (-not $event.Cancel) { throw 'Close ignored pending writes from the previous Settings view model.' }
    $secondEvent=[ComponentModel.CancelEventArgs]::new()
    [void]$closing.Invoke($window,@($window,$secondEvent))
    if (-not $secondEvent.Cancel) { throw 'Repeated close bypassed the pending drain.' }
    $local.SetResult($true); $shared.SetResult($true)
    $drainTime=Wait-FixtureClose $window 2000
    Write-Output ('PASS settings-reload owner retained; repeated close cancelled; completed local/shared writes drain in {0} ms.' -f $drainTime)

    $offline=[EsapiRunnerHub.MainWindow]::new()
    $offlineVm=New-FixtureViewModel
    $offline.GetType().GetField('historyOwners',$flags).GetValue($offline).Add($offlineVm)
    $offline.DataContext=$offlineVm
    $stalled=[Threading.Tasks.TaskCompletionSource[bool]]::new()
    $offlineVm.GetType().GetField('historySynchronization',$flags).SetValue($offlineVm,$stalled.Task)
    $offlineEvent=[ComponentModel.CancelEventArgs]::new()
    [void]$offline.GetType().GetMethod('WindowClosing',$flags).Invoke($offline,@($offline,$offlineEvent))
    if (-not $offlineEvent.Cancel) { throw 'Shared synchronization did not get its bounded close opportunity.' }
    $offlineTime=Wait-FixtureClose $offline 8000
    if ($offlineTime -lt 4500 -or $offlineTime -gt 7500) { throw 'Shared-storage close timeout is outside its expected bound.' }
    Write-Output ('PASS stalled shared I/O allows close with confirmed local copy after {0} ms; no dialog or visible window.' -f $offlineTime)
}
finally { [Threading.SynchronizationContext]::SetSynchronizationContext($previousContext) }
