# Guía 05: Trabajador

| | |
|---|---|
| **Audiencia** | Personal de logística |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-01 |
| **Requisitos cubiertos** | RF-INV-002, RF-CON-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 90 min |

## Para qué sirve

Compras, ventas e inventario con idempotencia.

## Antes de empezar

Requisitos previos:

- [ ] Rol Trabajador

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Cree pedidos y recepciones; contabilice con cabecera `Idempotency-Key`.

2. Confirme ventas: reserva blanda ≤ ATP; el exceso queda «sin cubrir» con warning (diseño del producto.

3. Contabilice entregas (pedido Preparado + PrepPick confirmado); rechazo si stock negativo / FEFO insuficiente. Misma `Idempotency-Key` reintenta; clave con payload distinto → conflicto.

4. Putaway post-GR, traspasos entre ubicaciones, ajustes/reversión con motivo, devoluciones comerciales y HU/SSCC (packing list PDF lab).

5. Consulte libro de movimientos, lotes y OTIF.

## Resultado esperado

Stock alineado con movimientos.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Stock insuficiente al entregar` | Sin existencias | Recepcione o ajuste |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Al confirmar un pedido se reserva stock hasta el ATP disponible; la entrega
exige preparación confirmada. Crédito multiempresa y tipos de cambio pueden
estar limitados según instalación. Fuera de alcance habitual: SAP RFC
productivo y envío SIF AEAT real.

## Véase también

- [07 Excel](07-excel.md)
- [13 FAQ](13-faq-errores.md)
