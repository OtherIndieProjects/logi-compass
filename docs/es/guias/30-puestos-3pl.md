# Guía 30: Puestos 3PL por rango

| | |
|---|---|
| **Audiencia** | Formación / FP |
| **Versión** | diseño del producto
| **Última revisión** | 2026-11-01 |

## Mapa rápido

| Rango | Módulos principales |
|---|---|
| Ventas | Ventas (ATP warnings al confirmar), Facturas (emitir/rectificar) |
| Administrativo | Oficina (CMR/albarán + sello PNG), cierre pedido, impresión facturas |
| Asistente / Operario | Preparación (locks + picking confirmado antes de Prepared) |
| Coordinador | Almacenes/ubicaciones/putaway con lote, inventario, baja material 4 ojos |
| Gerente | Todo interno (no portal Cliente) |
| Admin | Todo + config fiscal/Verifactu/rangos; `SetStockStatus` (RF-INV-002 Parcial) |
| Cliente | Portal: pedidos y facturas propias |

## Flujo de pedido

1. Ventas confirma pedido (reserva ATP; avisos si falta stock).
2. Operario/Asistente entra en preparación (Exclusive u Open) y confirma picks.
3. Marca preparado (exige picks confirmados).
4. Administrativo cierra y emite CMR/albarán (sello PNG del emisor si configurado).
5. Ventas emite factura fiscal (SIF + QR LocalChain).

## Véase también

- [14 Rangos y permisos](14-roles-permisos.md)
- la documentación de arquitectura interna
- la documentación de arquitectura interna
