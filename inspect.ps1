$dll = (Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.agents.ai\1.22.0\lib" -Filter "Microsoft.Agents.AI.dll" -Recurse | Select-Object -First 1).FullName
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
foreach ($t in $asm.GetExportedTypes()) {
    if ($t.Name -like "*ChatClient*") {
        Write-Output "TYPE: $($t.FullName)"
        Write-Output "PROPERTIES:"
        foreach ($p in $t.GetProperties()) {
            Write-Output "  $($p.PropertyType.Name) $($p.Name)"
        }
        Write-Output "CONSTRUCTORS:"
        foreach ($c in $t.GetConstructors()) {
            Write-Output "  $($c.ToString())"
        }
    }
}
