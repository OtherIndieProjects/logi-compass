# Guía 24: Recuperación ante desastres

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | RNF-REC-002  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 90 min |

## Para qué sirve

Recuperación completa tras incidente.

## Antes de empezar

Requisitos previos:

- [ ] Backup `.enc` y claves

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Nuevo servidor + Postgres.

2. Restore con YES.

3. Restaure claves JWT/backup/PII.

4. verify-audit + conciliación stock.

## Resultado esperado

Servicio restablecido.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Ensayo periódico recomendado.

## Véase también

- [08 Backups](08-backups-recuperacion.md)
