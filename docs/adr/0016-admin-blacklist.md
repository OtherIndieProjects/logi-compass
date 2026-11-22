# ADR 0016: Lista negra absoluta del editor administrativo

- **Estado:** Aceptado
- **Fecha:** 2026-10-31
- **Referencias del plan:** `docs/es/PLAN.md`
- **Requisitos afectados:** SEG-EDT-001
- **Implementación:** `AdminTableBlacklist` en Domain; ausencia de endpoints
  genéricos de edición de tablas en Api (`LayerDependencyTests`)

## Contexto

Un editor de filas sobre PostgreSQL permitiría a un administrador alterar el
ledger, tokens, hashes de auditoría o cantidades contabilizadas. El plan exige
lista negra **absoluta**, incluso para el rol Admin.

## Decisión

1. `AdminTableBlacklist.Tables` y `.Columns` definen tablas y columnas
   prohibidas para cualquier editor genérico futuro (p. ej. `stock_balances`,
   `audit_log_entries`, `refresh_tokens`, columnas `quantity`, `entry_hash`, …).
2. No hay API de «editor de tabla» en v1.0; los maestros se mantienen por
   comandos y pantallas acotadas.
3. Cualquier herramienta administrativa nueva debe consultar
   `AdminTableBlacklist.IsForbidden` antes de exponer datos editables.

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| Editor con permisos «solo Admin» | Rechazada: el riesgo es el dato, no el rol. |
| Lista negra solo en UI | Rechazada: la API debe fallar igual. |

## Consecuencias

- Cambios en tablas prohibidas solo vía migraciones controladas o comandos de
  negocio diseñados al efecto.
- Las pruebas de arquitectura detectan nombres sospechosos (`TableEditor`, …).

## Verificación

- `AdminTableBlacklistTests`, `LayerDependencyTests.Domain_Defines_Admin_Table_Blacklist_Precondition`,
  `Api_Has_No_Generic_Table_Editor_Endpoints`.

## Impacto en documentación

Guía 4 (cap. permisos y lista negra), guía 9, `docs/es/requisitos.md` (SEG-EDT-001).
