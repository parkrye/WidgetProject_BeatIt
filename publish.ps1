# 배포용 단일 exe 를 만든다. 결과는 dist\BeatIt.exe 하나.
param(
    [string]$Output = "dist",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

dotnet publish src\BeatIt -c Release -r $Runtime --self-contained false -p:PublishSingleFile=true -o $Output --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem $Output | Select-Object Name, @{ Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 2) } }
