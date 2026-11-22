# SAP vs Logi Compass — matriz de flujo

**Producto:** AAA Logistics Studio / Logi Compass (oficina Avalonia)  
**Objetivo:** alinear mentalmente el flujo SAP (SO → entrega → pick → PGI) con nuestros menús.

## Cadena de salida

| Paso SAP (mental) | Qué hace | Dónde en Logi Compass |
| --- | --- | --- |
| Sales order (crear / confirmar) | Pedido del cliente | **VENTAS → Pedidos de venta** (Crear → Confirmar) |
| Outbound delivery (crear DN) | Documento de entrega | *No hay menú DN aparte* — nace al **Contabilizar entrega** en Pedidos de venta |
| Warehouse pick / TO | Recoger mercancía | **ALMACÉN → Preparación** (cola/sesión de oficina) |
| RF / escaneo en planta | Confirmar pick con pistola | **ALMACÉN → Almacén RF** (no sustituye Preparación) |
| PGI (goods issue) | Baja stock fiscal | Misma acción **Contabilizar entrega** en Pedidos de venta |
| Billing | Factura | **VENTAS → Facturas** |
| Stock / IM | Consulta y correcciones | **ALMACÉN → Inventario** (no es la cola de picking) |

## Preparación vs Almacén RF

| | **Preparación** | **Almacén RF** |
| --- | --- | --- |
| Grupo menú | ALMACÉN | ALMACÉN |
| Para quién | Oficina / jefe de almacén | Operario con pistola |
| Qué hace | Abrir sesión, cola, confirmar líneas, **Marcar preparado** | Escaneo SSCC, putaway, confirmar pick, conteo |
| Baja stock | No | No |
| Permiso puerta | `SalesOrder.Prepare` | Stock + Prepare (según acción) |

## Quién ve Preparación

- Rango **Almacén**: sí (`SalesOrder.Prepare` + `SalesOrder.Read`).
- Rango **Ventas**: sí (sigue teniendo Prepare; el **menú** está bajo ALMACÉN, no bajo VENTAS).
- Claim se mantiene `SalesOrder.Prepare` (no se renombra a Almacén.*).

## Flujo recomendado (novato)

1. **Ventas** → crear y **Confirmar** pedido.  
2. **ALMACÉN → Preparación** → Entrar → Confirmar recogida → **Marcar preparado**.  
3. (Opcional) **Almacén RF** si el picking se confirma en planta.  
4. **Ventas** → **Contabilizar entrega** (DN + PGI).  
5. **Facturas** / **Oficina · CMR** según haga falta.

## Qué no confundir

- **Inventario** ≠ picking: es stock/IM.  
- **Contabilizar entrega** hace DN + PGI juntos (no hay paso “crear entrega” separado).  
- **CMR** documenta transporte; **no** baja stock.
