$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))

$log = New-Object System.Collections.Generic.List[string]
[AppDomain]::CurrentDomain.add_AssemblyResolve({
  param($s, $e)
  $log.Add("RESOLVE $($e.Name)")
  return $null
})

$asm = [Reflection.Assembly]::LoadFile((Join-Path $nw "Plugins\NavisworksExport.Glb.2026\NavisworksExport.Glb.2026.dll"))
try {
  "GLB GetTypes $($asm.GetTypes().Count)"
} catch [Reflection.ReflectionTypeLoadException] {
  "GLB FAIL"
  $_.Exception.LoaderExceptions | ForEach-Object { $_.Message }
}

"Assemblies:"
[AppDomain]::CurrentDomain.GetAssemblies() |
  Where-Object { $_.GetName().Name -match 'Geometry|SharpGLTF|Glb|Memory|Unsafe' } |
  ForEach-Object { "$($_.FullName) | $($_.Location)" }

"Resolve events:"
$log
