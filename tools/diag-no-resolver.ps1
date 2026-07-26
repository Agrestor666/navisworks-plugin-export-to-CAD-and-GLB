$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
$cadDir = Join-Path $nw "Plugins\NavisworksExport.AutoCad.2026"
$glbDir = Join-Path $nw "Plugins\NavisworksExport.Glb.2026"

[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "System.Runtime.CompilerServices.Unsafe.dll"))

function Bare-GetTypes([string]$dll) {
  Write-Host "`n======== Bare LoadFile+GetTypes: $(Split-Path $dll -Leaf) ========"
  try {
    $asm = [Reflection.Assembly]::LoadFile($dll)
    # Do NOT touch any type / Install — only GetTypes like the host
    $types = $asm.GetTypes()
    "OK count=$($types.Count)"
  } catch [Reflection.ReflectionTypeLoadException] {
    "FAIL ReflectionTypeLoadException"
    $_.Exception.LoaderExceptions | ForEach-Object { "LE: $($_.GetType().Name): $($_.Message)" }
    # Did module init run anyway? check log mtime
  } catch {
    "FAIL $($_.Exception.GetType().Name): $($_.Exception.Message)"
  }
}

$log = Join-Path $env:TEMP "NavisworksExport.AutoCad.2026.log"
if (Test-Path $log) { Remove-Item $log -Force }

Bare-GetTypes (Join-Path $cadDir "NavisworksExport.AutoCad.2026.dll")
if (Test-Path $log) {
  "AutoCad log after bare GetTypes:"
  Get-Content $log
} else {
  "AutoCad log NOT written - ModuleInitializer did NOT run during GetTypes"
}

Bare-GetTypes (Join-Path $glbDir "NavisworksExport.Glb.2026.dll")
