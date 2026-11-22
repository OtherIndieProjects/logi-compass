# ADR 0023: Alcance completo didáctico (FP / 3PL aula)

- **Estado:** Aceptado (partes OUT supersedidas)
- **Fecha:** 2026-11-01
- **Amplía:** [ADR 0022](0022-alcance-3pl-rangos-wms-fiscal.md)
- **Supercede:** [ADR 0018](0018-sin-reservas.md) (reservas blandas al confirmar)
- **Enmienda:** [ADR 0015](0015-solo-comandos-stock.md) (comandos adicionales de posting)
- **Partes OUT supersedidas por:** [ADR 0024](0024-apendice-lab-in.md) (apéndice lab) y [ADR 0025](0025-industrial-in.md) (industrial lab/staging)

## Contexto

El producto «Triple AAA Logistics Estudio» debe cubrir el MUST operativo 3PL
español y el currículo FP/universidad intro. El núcleo didáctico de este ADR
sigue siendo el aula 3PL; el plan Full ABC elevó el apéndice lab y capacidades
industriales a ADRs posteriores sin invalidar el IN de 0023.

## Decisión — entra (IN)

1. Seguridad P0: step-up para todos los rangos internos; SoD `SalesOrder.Read` vs
   `Create`; portal con claim en listados; PermissionGate UI.
2. Grano WMS: `LocationId` NOT NULL en saldos; `LocationId` en movimientos;
   locks por ubicación; transfer ubicación vía `PostingService`.
3. Prep: locks atómicos; `MarkPrepared` con membresía; entrega exige Preparado;
   PrepPick vinculante en DN Prepared (consumo qty; Admin `AllowFefoOverride`).
4. ATP / reserva blanda al confirmar (≤ ATP; exceso = warning «sin cubrir»);
   advisory `atp:` compartido con GR/putaway.
5. Lotes / FEFO / picking guiado; estados de stock; putaway post-GR.
6. Fiscal ES didáctico: serie con `FOR UPDATE`, SIF por serie, idempotencia,
   rectificativa con motivo/líneas, sello PNG o RF-DOC-010 Parcial.
7. RLS facturas (cabecera + líneas); blacklist tablas 3PL; SSCC + packing list;
   devoluciones comerciales como comandos posting; Incoterms/aduanas texto.
8. KPIs básicos; visor auditoría; VerifactuSubmit stub LocalChain.

## Decisión — fuera (OUT) del núcleo 0023

Las filas siguientes quedan **fuera del done-criteria didáctico 0023**. Varias
pasan a IN en ADRs posteriores (lab/staging, no «producción industrial»):

| Tema OUT 0023 | Estado posterior |
|---|---|
| Waves / UoM / MRP / RateCard / TMS | IN lab → [0024](0024-apendice-lab-in.md) |
| Holding multiempresa, crédito, FX, multi-dueño | IN lab → [0025](0025-industrial-in.md) |
| SAP mock, RF scan, SignalR, SMTP, HA runbook, AEAT HTTP bridge | IN lab → [0025](0025-industrial-in.md) |
| SAP RFC productivo, AEAT SIF real, RF hardware, HA cloud | Siguen OUT globales |

## Aula: single-owner warehouse (0023)

El flujo didáctico usa stock de casa (`OwnerCustomerId = Empty`). El grano
multi-dueño es opt-in industrial (0025), no requisito del aula básica.

## Consecuencias

- Guías 04/05/README deben citar 0023 IN + remisión a 0024/0025 para lab.
- Catálogo: no marcar **Implementado** industrial sin API usable + prueba + guía.
- Amenazas: oversell reserva, manipulación FEFO, carrera SIF; ver también
  [threat-model-c](../es/threat-model-c.md).
