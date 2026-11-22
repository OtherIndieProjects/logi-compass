# ADR 0015: Solo los comandos de contabilización mutan stock

- **Estado:** Aceptado (enmendado 2026-11-01 por [ADR 0023](0023-alcance-completo-didactico.md))
- **Fecha:** 2026-10-31
- **Referencias del plan:** `docs/es/PLAN.md`
- **Requisitos afectados:** RF-INV-002, RF-CON-001, RF-CON-002
- **Implementación:** `PostingService` — único punto que llama `StockBalance.Apply`

## Contexto

El saldo de inventario debe ser derivable del libro de movimientos. Cualquier
`UPDATE` directo sobre `stock_balances` rompe la trazabilidad y el modelo de
reversión.

## Decisión

Solo estos casos de uso modifican `StockBalance` y crean movimientos enlazados
**dentro de `PostingService`**:

1. contabilizar recepción (GR);
2. contabilizar entrega (DN);
3. contabilizar traspaso entre almacenes (dos patas);
4. contabilizar traspaso entre ubicaciones (dos patas; grano WMS);
5. contabilizar ajuste (incluye apertura y recuento cuando se modelan como ajuste);
6. contabilizar devolución comercial (ReturnIn / ReturnOut);
7. revertir un movimiento ya contabilizado.

El resto del código (consultas, confirmación/reserva blanda, registro, editor
administrativo, prep) **no** escribe cantidades en stock. La reserva blanda
([ADR 0023](0023-alcance-completo-didactico.md)) no muta `StockBalance`.

## Consecuencias

- Toda corrección pasa por contabilización o reversión con motivo cuando aplique.
- `AdminTableBlacklist` incluye `stock_balances` y `stock_movements`.
- Prueba de arquitectura: ningún `StockBalance.Apply` fuera de `PostingService`.

## Verificación

- `InventoryWorkflowTests`; test de arquitectura Apply.
- Guías 4 y 5; `docs/es/requisitos.md` (RF-INV-002).
