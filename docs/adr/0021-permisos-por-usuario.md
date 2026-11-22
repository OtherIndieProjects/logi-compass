# ADR 0021 — Permisos por usuario (Trabajador)

## Estado

Aceptado

## Contexto

Los roles Admin / Trabajador / Cliente aportan conjuntos iniciales de claims
`permission`. Operación real necesita que no todos los trabajadores tengan las
mismas capacidades (p. ej. `Stock.Adjust` solo para algunos).

## Decisión

1. **Admin** siempre recibe `AppPermissions.All` en el JWT; no editable.
2. **Trabajador/Cliente**: si el usuario tiene claims `permission` en
   `identity_user_claims`, esas son las efectivas; si no, las del rol.
3. APIs internas (Admin): listar/crear trabajadores, GET/PUT permisos; mutaciones
   con step-up TOTP; al guardar se renueva security stamp y se revocan refresh
   tokens.
4. `GET /me` incluye `permissions` para que el cliente oculte navegación y
   acciones. El servidor sigue autorizando con políticas.

## Consecuencias

- Diferenciación operativa sin inventar un sistema RBAC aparte de Identity.
- Un trabajador con permisos personalizados deja de heredar cambios futuros del
  rol hasta que un Admin reasigne el conjunto.
- El cliente debe tratar 403 como “sin permiso” aunque la UI filtre.
