# Arquitectura — Logi Compass / AAA Logistics Studio

Vista para desarrolladores. Complementa el [`PLAN.md`](PLAN.md).

## Capas

```
AAALogistics.Client (Avalonia)  ──HTTP──►  Host :5088  /api/v1/internal
AAALogistics.Portal (Blazor)    ──HTTP──►  Host :5089  /api/v1/portal

Host
 ├── Api          (endpoints Minimal API)
 ├── Application   (casos de uso)
 ├── Domain        (entidades y reglas)
 ├── Infrastructure(EF Core, PDF, Identity, correo…)
 ├── Integrations  (SAP OData opt-in)
 └── Contracts    (DTOs / rutas compartidas)
```

Reglas de dependencia (también en tests de arquitectura):

- Domain no depende de nadie de infraestructura.
- Application depende de Domain.
- Infrastructure e Integrations implementan puertos de Application.
- Api/Host cablean todo; el Cliente no referencia Domain.

## Superficies HTTP

| Puerto | Quién | Prefijo |
| --- | --- | --- |
| `5088` | Cliente oficina, integradores | `/api/v1/internal` |
| `5089` | Portal Blazor | `/api/v1/portal` |

Ambos escuchan en `127.0.0.1` por defecto. Salud: `/health/live`, `/health/ready`.  
Documentación interactiva interna: `http://127.0.0.1:5088/scalar`.

El middleware de aislamiento impide mezclar rutas internal/portal entre listeners.

## Datos

- PostgreSQL con rol de aplicación de mínimo privilegio.
- Migraciones EF Core en Infrastructure.
- RLS para filas de cliente en escenarios portal.
- Stock: único camino de mutación = comandos de `PostingService` (recepción, entrega, traspaso, ajuste, etc.) con idempotencia.

## Documentos PDF

Generación propia (ISO 32000) en Infrastructure (`CommercialPdf`, informes).  
Cabecera de marca del producto (`/branding`) distinta del perfil fiscal emisor (`/issuer-fiscal`).

## Despliegue local

1. PostgreSQL en marcha (Docker `deploy/docker-compose.yml` o instancia propia).
2. `dotnet run --project src/AAALogistics.Host` o `Iniciar Servidor.bat` en LogisticSolution.
3. Cliente Avalonia contra `5088`.

Publicación Windows: script `tools/publish-logistic-solution.ps1` → carpeta Escritorio `LogisticSolution`.
