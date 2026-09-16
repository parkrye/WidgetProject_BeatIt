# 배포용 exe 두 개를 루트에 만든다.
#   BeatIt.exe            가볍다(약 2MB). 받는 쪽에 .NET 10 Desktop Runtime 이 있어야 한다.
#   BeatIt-standalone.exe 런타임까지 들어 있다. 아무것도 설치 안 해도 바로 돈다.
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Publish-Variant {
    param(
        [bool]$SelfContained,
        [string]$StageName,
        [string]$FinalName
    )

    $stage = Join-Path "artifacts" $StageName
    $arguments = @(
        "publish", "src\BeatIt",
        "-c", "Release",
        "-r", $Runtime,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $stage,
        "--nologo", "-v", "quiet"
    )

    if ($SelfContained) {
        $arguments += "--self-contained"
        $arguments += "-p:EnableCompressionInSingleFile=true"
    }
    else {
        # "--self-contained false" 는 값이 무시되는 경우가 있어 전용 스위치를 쓴다.
        $arguments += "--no-self-contained"
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "publish 실패: $FinalName" }

    Copy-Item (Join-Path $stage "BeatIt.exe") (Join-Path "." $FinalName) -Force
}

Publish-Variant -SelfContained $false -StageName "framework-dependent" -FinalName "BeatIt.exe"
Publish-Variant -SelfContained $true -StageName "standalone" -FinalName "BeatIt-standalone.exe"

Get-ChildItem "BeatIt.exe", "BeatIt-standalone.exe" |
    Select-Object Name, @{ Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 2) } }
