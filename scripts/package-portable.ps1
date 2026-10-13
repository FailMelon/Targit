param(
    [string]$Runtime = 'win-x64',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $taskRoot "artifacts/GitLlama-single-file-$Runtime"
}
$taskProject = Join-Path $taskRoot 'src/GitLlama.Desktop/GitLlama.Desktop.csproj'
& dotnet publish $taskProject -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed with exit code $LASTEXITCODE." }
