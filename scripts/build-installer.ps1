param([string]$OutputName = 'LA批量打印-修订6-安装程序.exe')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$stage = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'payload'
$null = New-Item -ItemType Directory -Path $payload -Force
foreach ($pair in @(@('bin','zw'), @('bin-acad','acad48'), @('bin-acad2025-2027','acad8'))) {
    $source = Join-Path $repo $pair[0]
    if (!(Test-Path $source)) { throw "缺少构建目录：$source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $payload $pair[1]) -Recurse
}
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repo 'docs\图片签章版使用说明.md') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repo 'docs\修订4-功能对照与使用.md') -Destination $payload
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $stage 'payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payload, $zip)
$release = Join-Path $repo 'release'
$null = New-Item -ItemType Directory -Path $release -Force
$output = Join-Path $release $OutputName
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ "/out:$output" "/resource:$zip,payload.zip" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $repo 'installer\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw '安装程序编译失败' }
Write-Host "安装包：$output"
