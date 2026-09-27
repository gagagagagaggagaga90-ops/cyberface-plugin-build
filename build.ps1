$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$stubOut = Join-Path $root 'stubbin'
$outDir = Join-Path $root 'out'
New-Item -ItemType Directory -Path $stubOut -Force | Out-Null
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (!(Test-Path $csc)) { throw "Missing .NET Framework C# compiler: $csc" }

$pf = Join-Path $framework 'WPF\PresentationFramework.dll'
$pc = Join-Path $framework 'WPF\PresentationCore.dll'
$wb = Join-Path $framework 'WPF\WindowsBase.dll'
$sx = Join-Path $framework 'System.Xaml.dll'
$xml = Join-Path $framework 'System.Xml.Linq.dll'
$zip = Join-Path $framework 'System.IO.Compression.dll'
$zipfs = Join-Path $framework 'System.IO.Compression.FileSystem.dll'

& $csc /nologo /target:library /platform:x64 /optimize+ /out:"$stubOut\FrostyCore.dll" /reference:"$pf" /reference:"$pc" /reference:"$wb" "$root\stubs\FrostyCoreStub.cs"
if ($LASTEXITCODE -ne 0) { throw 'FrostyCore stub compilation failed.' }

$out = Join-Path $outDir 'CyberfaceSpreadsheetImporter.dll'
$args = @(
    '/nologo',
    '/target:library',
    '/platform:x64',
    '/optimize+',
    '/warn:4',
    '/langversion:5',
    ('/out:' + $out),
    ('/reference:' + (Join-Path $stubOut 'FrostyCore.dll')),
    ('/reference:' + $pf),
    ('/reference:' + $pc),
    ('/reference:' + $wb),
    ('/reference:' + $sx),
    ('/reference:' + $xml),
    ('/reference:' + $zip),
    ('/reference:' + $zipfs),
    (Join-Path $root 'CyberfaceSpreadsheetImporter.cs')
)

& $csc @args
if ($LASTEXITCODE -ne 0) { throw 'CyberfaceSpreadsheetImporter compilation failed.' }

Write-Host "Built $out"
Get-Item $out | Format-List FullName,Length,LastWriteTime
