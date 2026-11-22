# AAA Logistics Studio (Logi Compass)

Sistema de oficina logística de escritorio: compras, almacén, ventas, facturación ES, auditoría y módulos Studio.

| Pieza | Tecnología |
| --- | --- |
| Servidor | .NET 10 + PostgreSQL (API en `5088` / portal `5089`) |
| Cliente | Avalonia (Windows) |
| Portal | Blazor (opcional) |

Licencia MIT · Repositorio: [OtherIndieProjects/logi-compass](https://github.com/OtherIndieProjects/logi-compass)

## Descargar (Windows)

Paquete listo para ejecutar (Cliente + Servidor + Portal + guías PDF):

- [Última release](https://github.com/OtherIndieProjects/logi-compass/releases/latest) → `LogisticSolution-Windows-x64.zip`
- Instrucciones: [`download/README.md`](download/README.md)

Tras extraer: `Iniciar Servidor.bat` → `Iniciar Cliente.bat`.

## Compilar desde el código

Requisitos: .NET 10 SDK y PostgreSQL.

```bash
dotnet restore AAALogistics.slnx
dotnet build AAALogistics.slnx
dotnet test AAALogistics.slnx

# Base de datos local (ver deploy/README.md)
cp deploy/.env.example deploy/.env   # edite contraseñas
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d postgres

dotnet run --project src/AAALogistics.Host
dotnet run --project src/AAALogistics.Client
```

Primera vez — administrador:

```bash
dotnet run --project src/AAALogistics.Host -- create-admin --user admin --password "UNA_FRASE_LARGA_LOCAL"
```

API interactiva: `http://127.0.0.1:5088/scalar`

## Documentación

| Documento | Contenido |
| --- | --- |
| [`docs/Guia-Logi-Compass.pdf`](docs/Guia-Logi-Compass.pdf) | Guía novato |
| [`docs/es/api-guia.md`](docs/es/api-guia.md) · [PDF API](docs/Guia-API-Logi-Compass.pdf) | Integrar apps propias |
| [`docs/es/PLAN.md`](docs/es/PLAN.md) · [`docs/es/arquitectura.md`](docs/es/arquitectura.md) | Plan y arquitectura |
| [`deploy/README.md`](deploy/README.md) | Docker, restore, systemd |
| [`docs/es/guias/`](docs/es/guias/) | Guías por tema |
| [`CHANGELOG.md`](CHANGELOG.md) | Historial de versiones |

## Contribuir y seguridad

- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`SECURITY.md`](SECURITY.md) — vulnerabilidades solo por reporte privado de GitHub
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)

## Licencia

MIT. Ver [LICENSE](LICENSE) y [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
