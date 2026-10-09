# Arranca la mascota de escritorio de Artemis en segundo plano.
# Uso: .\DesktopMascot\run-mascot.ps1
# El motor Godot no se commitea: se toma de $env:GODOT_EXE o, si no está
# definida, de la ubicación por defecto en Documents\Tools.
# (El Godot_*_console.exe falla en este equipo con "CreateProcess error 193";
# se usa el .exe principal.)

$godot = $env:GODOT_EXE
if (-not $godot) {
    $godot = Join-Path $HOME "Documents\Tools\godot-engine\Godot_v4.7.2-stable_win64.exe"
}
if (-not (Test-Path $godot)) {
    Write-Error "No se encontró Godot en '$godot'. Define `$env:GODOT_EXE con la ruta al ejecutable."
    exit 1
}

Start-Process -FilePath $godot -ArgumentList @("--path", "`"$PSScriptRoot`"")
