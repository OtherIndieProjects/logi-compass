# Guía 27: Estado y alertas

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | —  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 20 min |

## Para qué sirve

Monitorización operativa.

## Antes de empezar

Requisitos previos:

- [ ] Acceso acorde al rol descrito.

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Monitore `/health/*`.

2. Alerte fallos backup.

3. Espacio en disco `Data/backups`.

## Resultado esperado

Alertas antes de saturación.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Algunas funciones avanzadas pueden no estar disponibles en todas las instalaciones.

## Véase también

- [03 Admin sistema](03-administrador-sistema.md)
