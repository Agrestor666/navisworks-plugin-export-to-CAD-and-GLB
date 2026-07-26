$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
$cadDir = Join-Path $nw "Plugins\NavisworksExport.AutoCad.2026"
$glbDir = Join-Path $nw "Plugins\NavisworksExport.Glb.2026"

# Preload like Roamer: API + host Unsafe
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "System.Runtime.CompilerServices.Unsafe.dll"))

function Test-Plugin([string]$dir, [string]$dllName) {
  Write-Host "`n======== $dllName ========"
  $log = New-Object System.Collections.Generic.List[string]
  $handler = [ResolveEventHandler] {
    param($s, $e)
    try {
      $an = New-Object Reflection.AssemblyName($e.Name)
      $name = $an.Name
      if ([string]::IsNullOrEmpty($name)) { return $null }
      foreach ($probe in @($dir, $nw)) {
        $cand = Join-Path $probe ($name + ".dll")
        if (Test-Path $cand) {
          try {
            $log.Add("TRY $name req=$($an.Version) from $cand")
            return [Reflection.Assembly]::LoadFrom($cand)
          } catch {
            $log.Add("FAILLOAD $name :: $($_.Exception.Message)")
          }
        }
      }
      # unify: return already-loaded same simple name
      $existing = [AppDomain]::CurrentDomain.GetAssemblies() |
        Where-Object { $_.GetName().Name -eq $name } |
        Select-Object -First 1
      if ($existing) {
        $log.Add("UNIFY $name have=$($existing.GetName().Version) req=$($an.Version)")
        return $existing
      }
      $log.Add("MISS $name req=$($an.Version)")
    } catch {
      $log.Add("ERR $($e.Name) :: $($_.Exception.Message)")
    }
    return $null
  }
  [AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
  try {
    $dll = Join-Path $dir $dllName
    $asm = [Reflection.Assembly]::LoadFile($dll)
    try {
      $n = $asm.GetTypes().Count
      "GetTypes OK count=$n"
    } catch [Reflection.ReflectionTypeLoadException] {
      "GetTypes FAIL"
      $_.Exception.LoaderExceptions | ForEach-Object { "LE: $($_.GetType().Name): $($_.Message)" }
    }
  } finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($handler)
  }
  "Resolve log:"
  $log | Select-Object -First 30
}

# Important: test AutoCAD FIRST in a fresh process - this script is one process.
# Run AutoCAD only here; GLB in separate invoke below via jobs.

Test-Plugin $cadDir "NavisworksExport.AutoCad.2026.dll"

Write-Host "`n=== Domain after AutoCAD attempt ==="
[AppDomain]::CurrentDomain.GetAssemblies() |
  Where-Object { $_.GetName().Name -match 'Unsafe|Memory|ACadSharp|SharpGLTF|AutoCad|Glb' } |
  ForEach-Object { "$($_.GetName().Name) $($_.GetName().Version) | $($_.Location)" }
