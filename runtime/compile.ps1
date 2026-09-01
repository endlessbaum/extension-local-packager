param(
  [Parameter(Mandatory=$true)][string]$SourceDir,
  [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$commonRefs = @('System.dll', 'System.Core.dll', 'System.Web.Extensions.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
Add-Type -Path (Join-Path $SourceDir 'Updater.cs') -ReferencedAssemblies $commonRefs -OutputAssembly (Join-Path $OutputDir 'Updater.exe') -OutputType ConsoleApplication
Add-Type -Path (Join-Path $SourceDir 'Uninstall.cs') -ReferencedAssemblies @('System.dll') -OutputAssembly (Join-Path $OutputDir 'Uninstall.exe') -OutputType ConsoleApplication
$updater = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $OutputDir 'Updater.exe')))
$uninstaller = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $OutputDir 'Uninstall.exe')))
$setupSource = [IO.File]::ReadAllText((Join-Path $SourceDir 'Setup.cs')).Replace('{{UPDATER_BASE64}}', $updater).Replace('{{UNINSTALLER_BASE64}}', $uninstaller)
$generatedSetup = Join-Path $SourceDir 'Setup.generated.cs'
[IO.File]::WriteAllText($generatedSetup, $setupSource, [Text.UTF8Encoding]::new($false))
$setupRefs = $commonRefs + @('System.Windows.Forms.dll')
Add-Type -Path $generatedSetup -ReferencedAssemblies $setupRefs -OutputAssembly (Join-Path $OutputDir 'Setup.exe') -OutputType ConsoleApplication
Remove-Item (Join-Path $OutputDir 'Updater.exe') -Force
Write-Host "Created $(Join-Path $OutputDir 'Setup.exe')"
Write-Host "Created $(Join-Path $OutputDir 'Uninstall.exe')"
