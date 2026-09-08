$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\PdfEditor.Desktop\PdfEditor.Desktop.csproj'
$publish = Join-Path $PSScriptRoot 'publish\win-x64'

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publish | Out-Null

Invoke-DotNet restore $project

# IMPORTANT: publish as a normal folder, not a single-file bundle.
# PdfiumViewer uses the native pdfium.dll runtime asset. Keeping the native DLL
# beside the executable is the most reliable deployment layout for this MVP.
Invoke-DotNet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false --output $publish

$exe = Join-Path $publish 'PdfTextEditor.exe'
if (-not (Test-Path $exe)) {
    throw "Publish completed without creating $exe"
}

$pdfium = Get-ChildItem -Path $publish -Filter 'pdfium.dll' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $pdfium) {
    throw "Publish completed, but native pdfium.dll was not found. The PDF engine cannot run without it."
}

Write-Host "Published to: $publish" -ForegroundColor Green
Write-Host "Native PDFium: $($pdfium.FullName)" -ForegroundColor Green
Write-Host "Run PdfTextEditor.exe from this folder. Keep all DLL files beside it." -ForegroundColor Green
