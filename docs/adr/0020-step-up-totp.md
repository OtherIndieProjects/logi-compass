# ADR 0020 — Step-up TOTP en operaciones críticas

## Estado

**Retirado.** El producto actual autentica con usuario + contraseña + rangos/permisos (estilo ERP). No hay TOTP de aplicación ni step-up.

## Contexto (histórico)

Se exploró un segundo factor TOTP en mutaciones críticas internas, separado del login diario.

## Decisión vigente

- Login / refresh / logout: usuario + contraseña.
- Autorización: rangos y `AppPermissions`.
- Los endpoints `/auth/2fa/*` y `/auth/step-up` responden códigos `AAA-AUTH-*-REMOVED`.
- RF-IDN-002 queda marcado como **Retirado** en `docs/es/requisitos.md`.

## Consecuencias

- Menos fricción operativa; la seguridad crítica se apoya en RBAC, auditoría e idempotencia.
- Este ADR se conserva solo como histórico.
