# ADR 0018: Confirmación de venta sin reserva de stock en v1.0

- **Estado:** Superado
- **Fecha:** 2026-10-31
- **Supercedido por:** [ADR 0023](0023-alcance-completo-didactico.md) (2026-11-01)
- **Referencias del plan:** `docs/es/PLAN.md`

## Contexto histórico

v1.0 optó por cantidad abierta como demanda pendiente sin reserva blanda.

## Decisión original (obsoleta)

`ConfirmarPedidoVenta` no reservaba ni descontaba stock; solo advertía.

## Estado actual

Bajo ADR 0023, la confirmación crea `StockReservation` hasta ATP disponible;
el exceso queda «sin cubrir» con warning. La entrega consume reserva + saldo
bajo lock. El portal expone ATP informativo, no garantía de plazo.
