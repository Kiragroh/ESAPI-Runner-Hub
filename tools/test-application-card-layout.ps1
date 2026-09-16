[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunnerAssembly,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

# Offscreen WPF rendering of the real card template with synthetic data only.
# No Window, ESAPI directory, Citrix launch, or target executable is started.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($RunnerAssembly))
$repoRoot = Split-Path -Parent $PSScriptRoot
[xml]$xaml = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'src\ESAPI.RunnerHub\MainWindow.xaml')
$ns = [Xml.XmlNamespaceManager]::new($xaml.NameTable)
$ns.AddNamespace('p', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$resourcesNode = $xaml.SelectSingleNode('/p:Window/p:Window.Resources', $ns)
$templateNode = $xaml.SelectSingleNode("//p:ItemsControl[@ItemsSource='{Binding VisibleApplications}']/p:ItemsControl.ItemTemplate/p:DataTemplate", $ns)
$namespaces = ' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"'
$resources = [Windows.Markup.XamlReader]::Parse('<ResourceDictionary' + $namespaces + '>' + $resourcesNode.InnerXml + '<DataTemplate x:Key="MetadataCardTemplate">' + $templateNode.InnerXml + '</DataTemplate></ResourceDictionary>')
$template = $resources['MetadataCardTemplate']
$panel = [Windows.Controls.StackPanel]::new()
$panel.Orientation = 'Horizontal'
$panel.Background = [Windows.Media.BrushConverter]::new().ConvertFromString('#F4F1EA')
$panel.Resources = $resources
$cards = @()
foreach ($sample in @(
    @{ Name='Current review preview'; Version='3.1.0-dev.20260908.2'; File='SyntheticReview.dll' },
    @{ Name='Unversioned source'; Version=''; File='SyntheticCheck.cs' }
)) {
    $definition = [EsapiRunnerHub.Configuration.ApplicationDefinition]::new()
    $definition.Id = $sample.File
    $definition.Name = $sample.Name
    $definition.Version = $sample.Version
    $definition.Category = 'Synthetic plan review'
    $definition.Description = 'Synthetic UI fixture. No clinical program or patient was loaded.'
    $definition.Executable = $sample.File
    $definition.AccessMode = [EsapiRunnerHub.Configuration.ApplicationAccessMode]::ReadOnly
    $card = [EsapiRunnerHub.ViewModels.ApplicationCardViewModel]::new($definition)
    $changed = [DateTime]::SpecifyKind([DateTime]'2026-09-08T10:11:12', [DateTimeKind]::Utc)
    $card.SetReadiness([EsapiRunnerHub.Launching.PathProbeResult]::new([EsapiRunnerHub.Launching.PathReadiness]::Ready, 'Ready', $null, $changed))
    $presenter = [Windows.Controls.ContentPresenter]::new()
    $presenter.Content = $card
    $presenter.ContentTemplate = $template
    [void]$panel.Children.Add($presenter)
    $cards += $card
}
try { $panel.Measure([Windows.Size]::new([double]::PositiveInfinity, [double]::PositiveInfinity)) }
catch {
    $detail = $_.Exception
    while ($detail.InnerException) { $detail = $detail.InnerException }
    throw $detail.Message
}
$panel.Arrange([Windows.Rect]::new([Windows.Point]::new(0, 0), $panel.DesiredSize))
$panel.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($panel.ActualWidth * 2), [int][Math]::Ceiling($panel.ActualHeight * 2), 192, 192, [Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($panel)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput))
$stream = [IO.File]::Create($resolvedOutput)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
if ($cards[0].VersionLabel -ne 'Version: 3.1.0-dev.20260908.2 (catalogue)') { throw 'Catalogue version label mismatch.' }
if ($cards[1].VersionLabel -ne 'Version: unavailable') { throw 'Missing version must remain explicit.' }
foreach ($card in $cards) {
    if ($card.LastChangedLabel -ne 'File changed: 2026-09-08 10:11:12 UTC') { throw 'UTC file timestamp label mismatch.' }
}
Write-Output ('PASS offscreen real card template: {0} x {1} DIP; {2}' -f $panel.ActualWidth, $panel.ActualHeight, $resolvedOutput)
