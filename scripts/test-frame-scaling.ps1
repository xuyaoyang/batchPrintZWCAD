param(
 [Parameter(Mandatory=$true)][string]$DotNet,
 [Parameter(Mandatory=$true)][string]$CoreConsole,
 [Parameter(Mandatory=$true)][string]$FixtureDwg,
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$null=New-Item -ItemType Directory -Force $OutputDirectory
$output=(Resolve-Path $OutputDirectory).Path
& $DotNet build (Join-Path $repo 'tests/FrameScaling/FrameScaling.csproj') -c Release -o $output --nologo
if($LASTEXITCODE -ne 0){throw 'Regression build failed'}
Copy-Item -LiteralPath $FixtureDwg -Destination (Join-Path $output 'fixture.dwg')
$probe=(Join-Path $output 'FrameScaling.dll').Replace('\','/')
$script=Join-Path $output 'run.scr'
@"
(command "_NETLOAD" "$probe")
LA_SCALE_CHECK
_QUIT
_Y
"@ | Set-Content $script -Encoding ascii
$process=Start-Process -FilePath $CoreConsole -ArgumentList ('/i "'+(Join-Path $output 'fixture.dwg')+'" /s "'+$script+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'host.log')
while(!$process.WaitForExit(1000)){if((Get-Date)-$process.StartTime -gt [TimeSpan]::FromMinutes(3)){throw 'CAD host timeout; inspect host.log'}}
$result=Get-Content (Join-Path $output 'scaling-result.json') -Raw | ConvertFrom-Json
$failed=@($result | Where-Object {!$_.boundary -or !$_.fields -or !$_.stamp -or $_.error})
if($result.Count -ne 32 -or $failed.Count){throw "Frame scaling regression failed: $($failed.Count)/$($result.Count)"}
Write-Host 'PASS: 32 polyline/line scale/insert/rotation cases; 96 boundary/text/stamp assertions'
