# Guía 08: Backups y recuperación

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | RNF-REC-001
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 40 min |

## Para qué sirve

Copias cifradas y restore controlado.

## Antes de empezar

Requisitos previos:

- [ ] `pg_dump`/`pg_restore`

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. `dotnet run --project src/AAALogistics.Host -- backup`.

2. Opcional `--gzip`.

3. Liste con `backup --list`.

4. Restaure: `restore --file …enc --confirm YES`.

## Resultado esperado

Artefacto `.enc` y hash SHA-256 en BD.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-BACKUP-0002` | Confirmación distinta de YES | Escriba YES exacto |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Drill de aula (backup / restore)

1. Crear un backup cifrado (`backup`) y anotar el hash SHA-256.
2. Contabilizar un movimiento de prueba (p. ej. ajuste menor).
3. Restaurar el dump (`restore --confirm YES`) y comprobar que el movimiento
   desaparece y `verify-audit` sigue OK.
4. Custodiar la clave `backup.key` **fuera** del servidor de BD (USB / cofre).

## Limitaciones

Sin PITR automático.

## Véase también

- [24 Recuperación](24-recuperacion-desastres.md)
- [23 Auditoría](23-auditoria.md)
