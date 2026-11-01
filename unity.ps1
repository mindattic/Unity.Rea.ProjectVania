# The scripted batch entry point for Unity 6000.4.3f1 (U.VAN-US-A3): compile, test or build this folder headless.
#   .\unity.ps1 compile            import and compile only
#   .\unity.ps1 test [-Filter X]   EditMode tests (results in Logs\editmode.xml)
#   .\unity.ps1 build              Windows player in Build\ProjectVania
#   .\unity.ps1 capture            renders the game camera to Logs\Capture\*.png (needs graphics)
# Never touches an editor the owner has open: it runs its own batchmode instance on this folder.
param(
    [Parameter(Mandatory = $true)][ValidateSet("compile", "test", "build", "capture")][string]$Mode,
    [string]$Filter = "",
    [string]$Output = "Build/ProjectVania/ProjectVania.exe"
)
$ErrorActionPreference = "Stop"
$project = $PSScriptRoot
$version = (Get-Content (Join-Path $project "ProjectSettings/ProjectVersion.txt") | Select-Object -First 1) -replace "m_EditorVersion:\s*", ""
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity" }
New-Item -ItemType Directory -Force (Join-Path $project "Logs") | Out-Null
$log = Join-Path $project "Logs/unity-$Mode.log"
$unityArgs = @("-batchmode", "-projectPath", $project, "-logFile", $log)
if ($Mode -ne "capture") { $unityArgs = @("-nographics") + $unityArgs }
switch ($Mode) {
    "capture" {
        $unityArgs += @("-executeMethod", "Unity.Rea.ProjectVania.EditorTools.SceneCapture.Capture", "-captureOutput", (Join-Path $project "Logs/Capture"))
    }
    "compile" { $unityArgs += @("-quit") }
    "test" {
        $unityArgs += @("-runTests", "-testPlatform", "EditMode", "-testResults", (Join-Path $project "Logs/editmode.xml"))
        if ($Filter) { $unityArgs += @("-testFilter", $Filter) }
    }
    "build" {
        $unityArgs += @("-quit", "-executeMethod", "Unity.Rea.ProjectVania.EditorTools.ProjectBuilder.Build", "-buildOutput", $Output)
    }
}
# -ArgumentList takes a string[] but Windows PowerShell 5.1 joins it with spaces and reparses it, so an
# unquoted element containing a space (a project path, -Output, -Filter) would split into two arguments.
# Quoting every element keeps each one intact regardless of what it contains.
$quotedArgs = $unityArgs | ForEach-Object { '"' + $_ + '"' }
$process = Start-Process -FilePath $unity -ArgumentList $quotedArgs -Wait -PassThru
Write-Host "Unity exited $($process.ExitCode); log: $log"
exit $process.ExitCode
