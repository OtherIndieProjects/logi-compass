# Publishes Cliente / Servidor / Portal into Desktop\LogisticSolution (local PC).
# Never writes passwords: credential lines already present in the local LEEME.txt are
# carried over as-is (they live only on this PC, never in the repository).
param(
  [switch]$StopRunning
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path ([Environment]::GetFolderPath("Desktop")) "LogisticSolution"

Write-Host "Publish root: $root"
Write-Host "Destination:  $dest"

$running = Get-Process -Name "AAALogistics.Server", "AAALogistics.Client", "AAALogistics.Portal" -ErrorAction SilentlyContinue |
  Where-Object { $_.Path -and $_.Path.StartsWith($dest, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
  if (-not $StopRunning) {
    $names = ($running | ForEach-Object { $_.Name }) -join ", "
    throw "Detenga primero $names (o ejecute con -StopRunning) antes de publicar."
  }

  $running | Stop-Process -Force
  Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
foreach ($folder in "Cliente", "Servidor", "Portal", "docs") {
  New-Item -ItemType Directory -Force -Path (Join-Path $dest $folder) | Out-Null
}

$projects = @(
  @{ Project = "src\AAALogistics.Client\AAALogistics.Client.csproj"; Output = "Cliente" },
  @{ Project = "src\AAALogistics.Host\AAALogistics.Host.csproj"; Output = "Servidor" },
  @{ Project = "src\AAALogistics.Portal\AAALogistics.Portal.csproj"; Output = "Portal" }
)
foreach ($item in $projects) {
  dotnet publish (Join-Path $root $item.Project) -c Release -o (Join-Path $dest $item.Output)
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló para $($item.Project) (código $LASTEXITCODE)."
  }
}

$guide = Join-Path $root "docs\Guia-Logi-Compass.pdf"
$apiGuide = Join-Path $root "docs\Guia-API-Logi-Compass.pdf"
if (Test-Path $guide) {
  Copy-Item $guide (Join-Path $dest "docs\Guia-Logi-Compass.pdf") -Force
}
if (Test-Path $apiGuide) {
  Copy-Item $apiGuide (Join-Path $dest "docs\Guia-API-Logi-Compass.pdf") -Force
}

$leemePath = Join-Path $dest "LEEME.txt"
$localCredentials = @()
if (Test-Path $leemePath) {
  $localCredentials = @(Get-Content $leemePath -Encoding UTF8 |
    Where-Object { $_ -match '^\s*(Usuario|Password|Contraseña)\s*:' })
}

$credentialBlock = if ($localCredentials.Count -gt 0) {
  "Credenciales locales (solo en este PC; cámbielas desde el cliente con" + [Environment]::NewLine +
  "«Cambiar contraseña» y borre estas líneas después)" + [Environment]::NewLine +
  ($localCredentials -join [Environment]::NewLine)
} else {
  "Credenciales: usuario y contraseña definidos en la instalación." + [Environment]::NewLine +
  "Este archivo no guarda contraseñas."
}

$leeme = @"
AAA Logistics Studio — LogisticSolution
=======================================
Logi Compass = brújula logística dentro del Studio.

USO
1) Iniciar Servidor.bat
2) Iniciar Cliente.bat
3) Guía usuario: docs\Guia-Logi-Compass.pdf
4) Guía API: docs\Guia-API-Logi-Compass.pdf
   OpenAPI/Scalar (Servidor interno): http://127.0.0.1:5088/scalar

Inicio de sesión
  Solo usuario y contraseña (como SAP ERP). Las operaciones críticas
  (contabilizar, facturar, bajas, usuarios, aprobaciones) usan roles/permisos
  y, si ya existía, una confirmación simple en pantalla — sin autenticador.

$credentialBlock

Studio (P0–P11)
  Menú izquierdo → STUDIO → Mapa Studio

SAP OData (opt-in)
  Edite Servidor\appsettings.json sección Sap o variables AAA_SAP_*.
  UI: Sistema → Integraciones SAP

APIs: 5088 interno / 5089 portal (solo 127.0.0.1)
"@
Set-Content -Path $leemePath -Value $leeme -Encoding UTF8
Write-Host "Publish completed."
