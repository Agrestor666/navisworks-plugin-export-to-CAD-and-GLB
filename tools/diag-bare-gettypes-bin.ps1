$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
$dll = "C:\Users\User\Documents\Cursor\navisworks-plugin-export-to-CAD\NavisworksExport.AutoCad.2026\bin\x64\Debug\net48\NavisworksExport.AutoCad.2026.dll"
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))
$log = Join-Path $env:TEMP "NavisworksExport.AutoCad.2026.log"
if (Test-Path $log) { Remove-Item $log -Force }
try {
  $asm = [Reflection.Assembly]::LoadFile($dll)
  $n = $asm.GetTypes().Count
  "BARE GetTypes OK count=$n"
} catch [Reflection.ReflectionTypeLoadException] {
  "BARE GetTypes FAIL"
  $_.Exception.LoaderExceptions | ForEach-Object { "LE: $($_.Message)" }
}
if (Test-Path $log) { "ModuleInit ran (log exists)" } else { "ModuleInit did not run (expected during GetTypes)" }
