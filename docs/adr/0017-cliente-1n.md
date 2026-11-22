# ADR 0017: Relación 1 Cliente : N usuarios portal

- **Estado:** Aceptado
- **Fecha:** 2026-10-31
- **Referencias del plan:** `docs/es/PLAN.md`
- **Requisitos afectados:** RF-IDN-003, SEG-RLS-001
- **Implementación:** `CustomerId` nullable en usuario Identity; login portal
  exige `CustomerId`; RLS con `app.customer_id`

## Contexto

Varios contactos de la misma empresa deben acceder al portal sin compartir
credenciales. El aislamiento entre empresas no puede depender de filtros solo
en la aplicación.

## Decisión

1. Entidad **Cliente** (empresa) con uno o más **usuarios** con rol `Cliente`
   y `CustomerId` obligatorio para tokens de audiencia portal.
2. Usuarios internos (`Admin`, `Trabajador`) tienen `CustomerId` nulo.
3. Las operaciones de portal aplican RLS PostgreSQL según el cliente del
   usuario autenticado ([ADR 0013](0013-rls-postgresql.md)).
4. La aprobación de registro crea Cliente + usuario Cliente en una transacción.

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| Un usuario por cliente | Rechazada: no escala operativamente. |
| Cliente implícito por email de dominio | Rechazada: ambigüedad y fugas. |

## Consecuencias

- Toda API portal debe validar propiedad del recurso y RLS.
- Altas de usuarios adicionales las gestiona el administrador de aplicación.

## Verificación

- `IdentityServices` (portal sin `CustomerId` rechazado).
- `SecurityGapTests.Portal_Customers_Cannot_Read_Each_Others_Sales_Orders`.

## Impacto en documentación

Guías 4, 6, 14 y 26; `docs/es/requisitos.md` (RF-IDN-003).
