$ErrorActionPreference = "Continue"
$nw = "C:\Program Files\Autodesk\Navisworks Manage 2026"
$cadDir = Join-Path $nw "Plugins\NavisworksExport.AutoCad.2026"

Write-Host "=== Host Unsafe ==="
$hostUnsafe = Join-Path $nw "System.Runtime.CompilerServices.Unsafe.dll"
[Reflection.AssemblyName]::GetAssemblyName($hostUnsafe).FullName
Write-Host "=== Plugin Unsafe ==="
$pluginUnsafe = Join-Path $cadDir "System.Runtime.CompilerServices.Unsafe.dll"
[Reflection.AssemblyName]::GetAssemblyName($pluginUnsafe).FullName

Write-Host "`n=== Preload Api + host Unsafe 6.0.0.0 ==="
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.Api.dll"))
[void][Reflection.Assembly]::LoadFrom((Join-Path $nw "Autodesk.Navisworks.ComApi.dll"))
[void][Reflection.Assembly]::LoadFrom($hostUnsafe)

$log = New-Object System.Collections.Generic.List[string]

# Exact mimic of PluginAssemblyResolver (always LoadFrom candidate, no existing check)
[AppDomain]::CurrentDomain.add_AssemblyResolve({
  param($s, $e)
  try {
    $an = New-Object Reflection.AssemblyName($e.Name)
    $name = $an.Name
    if ([string]::IsNullOrEmpty($name)) { return $null }
    $cand = Join-Path $cadDir ($name + ".dll")
    if (Test-Path $cand) {
      $log.Add("LOADFROM $name req=$($an.Version) file=$([Reflection.AssemblyName]::GetAssemblyName($cand).Version)")
      return [Reflection.Assembly]::LoadFrom($cand)
    }
    $log.Add("MISS $name req=$($an.Version)")
  } catch {
    $log.Add("ERR $($e.Name) :: $($_.Exception.Message)")
  }
  return $null
})

$dll = Join-Path $cadDir "NavisworksExport.AutoCad.2026.dll"
$asm = [Reflection.Assembly]::LoadFile($dll)
$resolver = $asm.GetType("NavisworksExport.AutoCad2026.PluginAssemblyResolver")
$resolver.GetMethod("Install").Invoke($null, @())

try {
  $types = $asm.GetTypes()
  "GetTypes OK $($types.Count)"
} catch [Reflection.ReflectionTypeLoadException] {
  "GetTypes FAIL"
  $_.Exception.LoaderExceptions | ForEach-Object { "LE: $($_.GetType().Name): $($_.Message)" }
}

Write-Host "`n=== Try load System.Memory from plugin (needs Unsafe 6.0.3) ==="
try {
  $sm = [Reflection.Assembly]::LoadFrom((Join-Path $cadDir "System.Memory.dll"))
  "Memory loaded: $($sm.FullName)"
  try {
    [void]$sm.GetType("System.Memory`1")
    "Memory touch OK"
  } catch {
    "Memory touch FAIL: $($_.Exception.Message)"
  }
} catch {
  "Memory LoadFrom FAIL: $($_.Exception.ToString())"
}

Write-Host "`n=== Resolve log ==="
$log

Write-Host "`n=== Domain assemblies of interest ==="
[AppDomain]::CurrentDomain.GetAssemblies() |
  Where-Object { $_.GetName().Name -match 'Unsafe|Memory|Buffers|ACadSharp|AutoCad|Vectors' } |
  ForEach-Object { "$($_.FullName) | $($_.Location)" }
