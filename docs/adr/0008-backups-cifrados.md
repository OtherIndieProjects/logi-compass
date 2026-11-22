# ADR 0008: Backups coherentes, falla cerrado y cifrado en reposo

- **Estado:** Aceptado
- **Fecha:** 2026-10-31
- **Spike relacionado:** S4 — OPEN (umbrales de tamaño); decisión de producto cerrada
- **Referencias del plan:** `docs/es/PLAN.md`
- **Requisitos afectados:** RNF-REC-001, RNF-REC-002
- **Implementación:** `BackupService`, `BackupEncryption` (AES-GCM, clave
  `Data/keys/backup.key`), registro en `backup_records`

## Contexto

Copias sin `pg_dump`, restauraciones accidentales o volcados en claro en disco
exponen la base completa. El plan exige snapshot coherente, confirmación
explícita al restaurar y cifrado del artefacto.

## Decisión

1. **Creación:** preferir `pg_dump` formato custom. Si `pg_dump` falta, la
   copia **falla cerrado** salvo `Backups:AllowSqlFallback=true` (SQL `COPY`).
2. **Cifrado:** el fichero entregado es `.enc` (AES-GCM) con clave local en
   `Backups:EncryptionKeyPath`; permisos `0600` en Unix.
3. **Integridad:** SHA-256 del ciphertext guardado en `backup_records`; el
   restore verifica el hash antes de descifrar.
4. **Restauración:** exige confirmación exacta `YES` (`AAA-BACKUP-0002` si no);
   sin `pg_restore` también falla cerrado.
5. **Metadatos:** cada ejecución queda registrada (tamaño, hash, error).

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| Solo SQL plano sin cifrar | Rechazada para artefactos persistentes. |
| Restaurar sin confirmación | Rechazada: riesgo operativo alto. |
| Cifrado opcional | Rechazada: copias locales deben ser seguras por defecto. |

## Consecuencias

- El instalador debe incluir cliente PostgreSQL (`pg_dump` / `pg_restore`).
- Backups anteriores a un crypto-shred pueden conservar claves PII; retención
  alineada con protección de datos (guía 8 y 18).

## Verificación

- `SecurityGapTests.Backup_Fails_Closed_When_PgDump_Is_Missing`.
- Comandos Host `backup`, `backup --list`, `restore --file … --confirm YES`.

## Impacto en documentación

Guías 8 y 24; spike S4 (tiempos de dump). Decisión de cifrado: este ADR.
