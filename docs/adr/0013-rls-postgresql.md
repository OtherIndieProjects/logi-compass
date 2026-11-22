# ADR 0013: RLS PostgreSQL con rol de aplicación sin BYPASSRLS

- **Estado:** Aceptado
- **Fecha:** 2026-10-31
- **Spike relacionado:** S3 — incorporado ([estado de spikes](../es/spikes/README.md))
- **Referencias del plan:** `docs/es/PLAN.md`
- **Implementación:** migración `SecurityHardening` + `TripleAaaSecurityClosure`,
  `ICustomerRlsSession`, `DatabaseRoleGuard`, `deploy/init-db.sh`

## Contexto

El aislamiento entre clientes del portal no puede depender solo de filtros EF:
hay que garantizarlo en PostgreSQL. `FORCE ROW LEVEL SECURITY` sujeta al dueño
de la tabla, pero **no** a un superusuario ni a un rol con `BYPASSRLS`. Si el
proceso de aplicación conecta como superusuario (comportamiento por defecto de
`POSTGRES_USER` en la imagen oficial), las políticas no filtran nada.

## Decisión

1. Rol de runtime `aaa_app`: `LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE`.
2. Rol `aaa_owner` (sin login) como dueño preferido de tablas sensibles en
   instalaciones nuevas (`deploy/init-db.sh`).
3. Políticas `customer_isolation` con `ENABLE` + `FORCE` en tablas de cliente,
   pedidos, entregas, solicitudes y `legal_acceptances`.
4. `SET LOCAL app.customer_id` vía `ICustomerRlsSession.ApplyAsync` al abrir
   transacciones de lectura/escritura.
5. Arranque **fail-closed**: si `rolsuper` o `rolbypassrls` es true, el Host
   aborta con `AAA-DB-ROLE-UNSAFE`.
6. Compose de referencia: bootstrap separado, password por `.env`, puerto
   Postgres solo en `127.0.0.1`.

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| Solo filtros EF | Rechazada: un `SELECT` directo o un bug de query salta el aislamiento. |
| RLS sin FORCE | Rechazada: el dueño de la tabla se saltaría las políticas. |
| App como superusuario «por comodidad» | Rechazada: invalida RLS por completo. |

## Consecuencias

- Las migraciones y el DML de aplicación deben ejecutarse con un rol sin
  privilegios peligrosos.
- Los tests de integración usan `aaa_app` sin superusuario y comprueban el
  aislamiento con `set_config` + `SELECT` directo.
- El despliegue de referencia ya no publica secretos ni `5432` en todas las
  interfaces.
