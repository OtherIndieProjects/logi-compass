# Modelo de amenazas — Tier C (industrial)

Fecha: 2026-11-01 · ADR [0025](../adr/0025-industrial-in.md)

## Activos

| Activo | Impacto |
|---|---|
| Stock multi-dueño (`OwnerCustomerId`) | Contaminación de inventario entre clientes 3PL |
| Crédito / bloqueo confirm | Bypass de límite → riesgo financiero |
| AEAT HTTP bridge | Fuga o reenvío fiscal incorrecto (no es SIF real) |
| SMTP / PoW | Abuso de registro y phishing |
| SignalR ops | Filtrado de eventos de almacén |

## Amenazas y controles

| ID | Amenaza | Control |
|---|---|---|
| T-C1 | JWT sin `company_id` omite crédito | Sin claim: se evalúan términos de **cualquier** company del customer; con claim: solo esa company |
| T-C2 | Manipulación OwnerCustomerId en posting | Default Empty (casa); ATP/FEFO/locks filtrados por owner; índice único grano |
| T-C3 | AEAT mal configurado en lab | Opt-in `Verifactu:Mode=AeatHttp`; LocalChain por defecto; RF-FAC-010 Parcial (JSON genérico) |
| T-C4 | Registro sin PoW cuando se exige | `Captcha:RequireProofOfWork` + endpoint challenge |
| T-C5 | Hub SignalR anónimo | `[Authorize]` en `OperationsHub`; posting notifica tras commit |
| T-C6 | SMTP credenciales en repo | Solo config/secretos; guía 16 alineada con `SmtpHost`/`SmtpFrom` |
| T-C7 | RF scan inventa resolución | 404 `AAA-RF-0002` si no hay HU/SSCC ni `Material.Code` |
| T-C8 | HA con env var incorrecta | Runbook usa `AAA_DISABLE_INSTANCE_LOCK`; JWT/DP keys compartidas |

## Residual aceptado (lab)

- SAP mock no cifra canal RFC real (RF-INT-001 Parcial).
- Company RLS aún claim-only (RF-TEN-001 Parcial).
- WCAG medido pendiente de informe axe en CI (RNF-ACC-001 Parcial).
- HA multi-región fuera de runbook aula (RNF-HA-001 Parcial).
- Wave release hace JoinPrep por SO con commit propio (RF-LAB-001 Parcial).
