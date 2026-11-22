# Guía 19: Actualizar y desinstalar

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | —  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 45 min |

## Para qué sirve

Versiones y retirada.

## Antes de empezar

Requisitos previos:

- [ ] Backup

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Detenga Host; backup; sustituya binarios; migraciones automáticas.

2. Desinstalar: export legal + borrar `Data/`.

## Resultado esperado

Cliente y servidor misma minor.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Algunas funciones avanzadas pueden no estar disponibles en todas las instalaciones.

## Véase también

- [10 Despliegue](10-despliegue-migracion.md)
