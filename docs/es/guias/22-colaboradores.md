# Guía 22: Guía para colaboradores

| | |
|---|---|
| **Audiencia** | Contribuidores |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-SEC-001, SEG-SUP-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Contribuir al repositorio.

## Antes de empezar

Requisitos previos:

- [ ] SDK .NET 10

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Lea [`CONTRIBUTING.md`](../../../CONTRIBUTING.md).

2. `dotnet test AAALogistics.slnx`.

3. Actualice requisitos, guía y ADR en PRs.

## Resultado esperado

CI verde.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Algunas funciones avanzadas pueden no estar disponibles en todas las instalaciones.

## Véase también

- [29 Plataformas](29-plataformas.md)
