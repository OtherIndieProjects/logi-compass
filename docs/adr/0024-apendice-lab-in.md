# ADR 0024: Apéndice de laboratorio IN (Waves / UoM / MRP / RateCard / TMS)

- **Estado:** Aceptado
- **Fecha:** 2026-11-01
- **Responsables:** equipo AAA Logistics
- **Referencias del plan:** módulo Lab didáctico
- **Requisitos afectados:** apéndice lab (antes diferido; ahora IN)

## Contexto

ADR 0023 dejó Waves, conversión UoM, MRP lite, RateCard 3PL y TMS (rutas) como lab diferido. Este ADR lo incorpora al producto didáctico (API, UI Lab y pruebas).

## Decisión

Incluimos en el producto didáctico (módulo Lab + API interna):

1. **Waves** — ola multi-pedido; liberación une preparación en modo Open y regenera picks.
2. **UoM convert** — factores entre `UnitOfMeasure` y conversión con redondeo `AwayFromZero` a la escala destino.
3. **MRP lite** — `PurchaseRequisition` desde mínimos de stock y huecos ATP vs demanda abierta.
4. **RateCard** — tarifas 3PL por servicio y cálculo de cargos (informe; sin forzar líneas de factura).
5. **TMS** — `TransportRoute` + paradas (DN/SO) y tarifa `Base + Km × RatePerKm`.

Quedan OUT de 0024 (pasan a ADR 0025): AEAT producción, SAP, RF hardware, HA, multiempresa, SMTP real, captcha/PoW, SignalR, WCAG medido.

## Alternativas consideradas

| Alternativa | A favor | En contra | Motivo del descarte |
|---|---|---|---|
| Mantener solo en apéndice diferido | Menos alcance | No cumple plan A+B | Descartado |
| TMS completo con tracking GPS | Realismo | Fuera de aula | Diferido industrial |

## Consecuencias

- **Positivas:** currículo FP cubre olas, MRP y tarifas sin salir del monolito.
- **Negativas:** más tablas y superficie API; Wave release acoplada a prep existente.
- **Mitigaciones:** UI aislada en «Lab apéndice»; permisos reutilizan Prepare / PO / Invoice / DeliveryNote.

## Verificación

- Migración `AppendixLabModules`
- `AppendixLabTests` (integración)
- Compilación Client + Host

## Impacto en documentación

- PLAN y requisitos: Lab incluido vía este ADR
- Pendientes industriales: ADR 0025
