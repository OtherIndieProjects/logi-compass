# Guía 03: Administrador del sistema

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-NET-001, RNF-REC-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 45 min |

## Para qué sirve

Operación diaria: salud, copias, claves y red.

## Antes de empezar

Requisitos previos:

- [ ] Guía 1 completada

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Revise `/health/ready` y registros del Host.

2. Confirme Postgres en red privada.

3. Ejecute backup cifrado periódico.

4. Ejecute `verify-audit` tras incidentes.

5. Custodie copias de `Data/keys/` fuera del servidor.

## Resultado esperado

Copias `.enc` registradas en `backup_records`.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-BACKUP-0001` | Sin `pg_dump` | Instale cliente PostgreSQL |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

SMTP y dominio: guías 16 y 17.

## Véase también

- [08 Backups](08-backups-recuperacion.md)
- [09 Seguridad](09-seguridad.md)
