# ADR 0025: Alcance industrial IN (lab/staging)

- **Estado:** Aceptado
- **Fecha:** 2026-11-01
- **Responsables:** equipo AAA Logistics
- **Referencias del plan:** capacidades industriales (histórico)
- **Requisitos afectados:** RF-FAC AEAT, RF-REG captcha, RNF-ACC, multiempresa, FX, HA

## Contexto

Tras ADR 0023 (didáctico) y 0024 (apéndice lab), el plan A+B+C exige capacidades industriales operables en lab/staging.

## Decisión

Incluimos:

**C1 plataforma**

- `Company` + claim JWT `company_id` + `ApplicationUser.CompanyId`
- Stock multi-dueño: `StockBalance.OwnerCustomerId` (Empty = operador)
- Condiciones comerciales / crédito con bloqueo en confirmación de venta
- Tipos de cambio `ExchangeRate` + conversión en factura/API

**C2 ops**

- Verifactu `AeatHttp` opt-in (`Verifactu:Mode=AeatHttp`; bridge lab, no payload SIF productivo)
- SAP mock (`ISapIntegrationClient`; materiales/push hardcodeados, no OData/RFC productivo)
- RF scan API (`/rf/scan`) para códigos/SSCC
- HA: runbook multi-instancia + Postgres (`AAA_DISABLE_INSTANCE_LOCK`)
- SMTP lab/staging (`Email:Mode=Smtp`; sin prueba E2E de entrega)
- Captcha PoW opt-in (`Captcha:RequireProofOfWork`; portal resuelve challenge)
- SignalR `OperationsHub` (`wh:{guid:D}`)
- Guía WCAG 2.2 AA medible

## Alternativas

| Alternativa | Motivo del descarte |
|---|---|
| Solo documentación OUT | No cumple plan C |
| AEAT obligatorio en lab | Rompe didáctico LocalChain |

## Verificación

Migración industrial; pruebas dominio crédito/FX; smoke integración; runbook HA; guía 31.

## Impacto documentación

PLAN, requisitos, threat-model C, apéndice → 0024/0025.
