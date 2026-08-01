[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$DestinationZip
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$destination = [System.IO.Path]::GetFullPath($DestinationZip)
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "ZIP source directory missing: $sourceRoot"
}
if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Force
}

$archive = [System.IO.Compression.ZipFile]::Open($destination, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Sort-Object FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($sourceRoot.Length + 1).Replace('\', '/')
        $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $input = [System.IO.File]::OpenRead($file.FullName)
        $output = $entry.Open()
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
            $input.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Created deterministic ZIP: $destination"
