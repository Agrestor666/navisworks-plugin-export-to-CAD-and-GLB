$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))

function Dump([string]$dll) {
  Write-Host "`n======== $(Split-Path $dll -Leaf) ========"
  $asm = [Reflection.Assembly]::LoadFile($dll)
  try {
    foreach ($t in $asm.GetTypes()) { "OK  $($t.FullName)" }
  } catch [Reflection.ReflectionTypeLoadException] {
    $ex = $_.Exception
    for ($i = 0; $i -lt $ex.Types.Length; $i++) {
      $t = $ex.Types[$i]
      $le = $ex.LoaderExceptions[$i]
      if ($t) { "OK  $($t.FullName)" }
      else { "BAD slot=$i LE=$($le.Message)" }
    }
    # Also list DefinedTypes via GetExportedTypes?
  }
  "Exported:"
  try { $asm.GetExportedTypes() | ForEach-Object { "EXP $($_.FullName)" } } catch [Reflection.ReflectionTypeLoadException] {
    "EXP FAIL"
    $_.Exception.LoaderExceptions | ForEach-Object { "  $($_.Message)" }
  }
}

Dump (Join-Path $nw "Plugins\NavisworksExport.AutoCad.2026\NavisworksExport.AutoCad.2026.dll")
Dump (Join-Path $nw "Plugins\NavisworksExport.Glb.2026\NavisworksExport.Glb.2026.dll")
