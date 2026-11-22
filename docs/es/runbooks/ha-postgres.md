# Runbook HA — Host multi-instancia + PostgreSQL

Ámbito: ADR [0025](../../adr/0025-industrial-in.md). Lab/staging.

## Host

1. Desactive el bloqueo de instancia única: `AAA_DISABLE_INSTANCE_LOCK=true` (constante `SingleInstanceLock.DisableEnvironmentVariable`).
2. Ejecute N réplicas detrás de un balanceador L4/L7 sticky opcional para SignalR (`/hubs/operations`).
3. JWT y DataProtection deben compartir claves entre réplicas (`Jwt:SigningKeyPath` / signing key compartida, carpeta Data Protection compartida).

## PostgreSQL

1. Primario + standby síncrono (o Patroni) con `synchronous_commit=on` para posting.
2. Connection string con failover hosts si el driver lo soporta.
3. Migraciones solo desde un job; réplicas de app no ejecutan `Migrate` concurrente.

## Verificación

- Dos instancias Host + un Postgres; login y post DN concurrentes sin stock negativo.
- SignalR: un cliente en cada instancia recibe `stockChanged` tras notificación.

## Límites lab

Sin operatoría cloud completa; este runbook es el mínimo operable en aula industrial.
