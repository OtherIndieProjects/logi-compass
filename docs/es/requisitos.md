# Catálogo de requisitos

Lista de requisitos funcionales (RF), no funcionales (RNF) y de seguridad (SEG)
del producto. Es la referencia para desarrollo y pruebas.

Estado general: **núcleo de oficina implementado**. Los RNF sin campaña de
medición formal siguen en Pendiente. Decisiones de alcance: `docs/adr/`.

## Convenciones

**Prefijos**

| Prefijo | Tipo |
|---|---|
| `RF-` | Requisito funcional |
| `RNF-` | Requisito no funcional (cifras objetivo; pendientes de medición formal) |
| `SEG-` | Requisito de seguridad |

Formato: `PREFIJO-ÁREA-NNN`. Áreas: `IDN` identidad, `REG` registro de clientes, `CAT` catálogo, `INV` inventario, `CON` contabilización, `COM` compras, `VEN` ventas, `AUD` auditoría, `BAK` backups, `INF` informes, `XLS` Excel, `EDT` editor administrativo, `NET` red, `SEC` secretos, `RLS` aislamiento, `REC` recuperación, `REN` rendimiento.

**Estados**

| Estado | Significado |
|---|---|
| Propuesto | Definido en el plan; sin implementar |
| En curso | Implementación iniciada |
| Implementado | Código fusionado; **exige prueba y guía** (convención de catálogo; CI ejecuta `dotnet test`, no valida la tríada automáticamente) |
| Parcial | Núcleo usable; faltan controles, grano o UI declarados en ADR 0023 |
| Verificado | Superado en las pruebas / smoke correspondientes |
| Pendiente | Solo RNF: cifra propuesta sin medir |
| Confirmado | Solo RNF: cifra medida y cumplida |

Las columnas `prueba` y `guia` enlazan al proyecto de pruebas y a la guía de usuario.

## Requisitos funcionales

| id | titulo | área | prueba | guia | estado |
|---|---|---|---|---|---|
| RF-IDN-001 | Inicio de sesión con usuario y contraseña, mensajes sin enumeración y bloqueo Identity (5 fallos / 15 min; no backoff progresivo) | Identidad | `SecurityGapTests`, `ScaffoldTests` | [26](guias/26-cuentas-sesiones.md) | Implementado |
| RF-IDN-002 | (Histórico) TOTP / step-up de aplicación — **retirado**; acceso = usuario + contraseña + rangos | Identidad | ADR [0020](../adr/0020-step-up-totp.md) (histórico) | [26](guias/26-cuentas-sesiones.md) | Retirado |
| RF-IDN-003 | Relación 1 Cliente : N Usuarios mediante `Usuario.ClienteId` | 3 | Domain.Tests; Integration.Tests | [14](guias/14-roles-permisos.md), [26](guias/26-cuentas-sesiones.md) | Implementado |
| RF-IDN-004 | Cierre de sesión propio y revocación/suspensión/baja administrativa con revocación de tokens | 1 | `SecurityGapTests` (`Logout_And_Suspend_*`) | [26](guias/26-cuentas-sesiones.md) | Implementado |
| RF-INV-001 | Libro `MovimientoStock` de solo adición; correcciones solo mediante operaciones explícitas | 3 | `InventoryWorkflowTests` | [4](guias/04-administrador-aplicacion.md), [5](guias/05-trabajador.md) | Implementado |
| RF-INV-002 | Solo comandos de `PostingService` (ADR 0015 enmendado) mutan `Stock` | 4 | `InventoryWorkflowTests`; ADR [0015](../adr/0015-solo-comandos-stock.md) | [4](guias/04-administrador-aplicacion.md), [5](guias/05-trabajador.md) | Parcial |
| RF-INV-003 | Rechazo de toda salida que produciría stock negativo, comprobada bajo bloqueo | 4 | `InventoryWorkflowTests` (flujo DN) | [5](guias/05-trabajador.md) | Parcial |
| RF-INV-004 | Conciliación `Stock = suma de movimientos` sin modificar datos (`reconcile-stock`) | 4 | `InventoryWorkflowTests` | [4](guias/04-administrador-aplicacion.md) | Implementado |
| RF-CON-001 | `Idempotency-Key` obligatoria en posting; misma clave → resultado guardado; payload distinto → conflicto | 4 | `InventoryWorkflowTests` (replay + conflicto) | [4](guias/04-administrador-aplicacion.md), [5](guias/05-trabajador.md) | Implementado |
| RF-CON-002 | Contabilización atómica: documento, líneas, movimientos, stock y estado derivado en una transacción | 4 | `InventoryWorkflowTests` | [5](guias/05-trabajador.md) | Implementado |
| RF-CON-003 | Ajuste, reversión y cancelación de cantidad abierta exigen motivo obligatorio | 4 | Integration / dominio | [4](guias/04-administrador-aplicacion.md), [5](guias/05-trabajador.md) | Implementado |
| RF-REG-001 | Solicitud de registro pública con límite por IP y honeypot (PoW opt-in = RF-REG-010) | 7 | `SecurityGapTests.Registration_*` | [6](guias/06-cliente-externo.md) | Parcial |
| RF-REG-002 | Estado inicial Pending/aprobación manual; rama `EmailPendiente`+verificación SMTP aún no modelada | 3 | Domain.Tests | [4](guias/04-administrador-aplicacion.md), [6](guias/06-cliente-externo.md) | Parcial |
| RF-REG-003 | Aprobación manual que crea o enlaza Cliente, Usuario y ContactoCliente en una transacción | 3 | Integration.Tests | [4](guias/04-administrador-aplicacion.md) | Implementado |
| RF-REG-004 | Rechazo con motivo obligatorio; expiración configurable (job/API en curso) | 3 | Domain.Tests | [4](guias/04-administrador-aplicacion.md) | Parcial |
| RF-IDN-005 | Rangos multi-asignables internos; Cliente exclusivo; unión de permisos JWT | 3PL | `RangoPermissionCatalogTests`; Integration.Tests | [14](guias/14-roles-permisos.md) | Implementado |
| RF-VEN-010 | Preparación con locks Exclusive/Open y estados EnPreparacion/Preparado; cierre Administrativo | 3PL | `RangoScopeTests` | [5](guias/05-trabajador.md), [30](guias/30-puestos-3pl.md) | Implementado |
| RF-INV-010 | Ubicaciones anidadas (`LocationNode` Zone/Column/Bin; `Warehouse.SiteKind` Campa/Nave) y stock por ubicación; transferencias vía posting | 3PL | `RangoScopeTests`, `InventoryWorkflowTests` | [4](guias/04-administrador-aplicacion.md) | Implementado |
| RF-INV-011 | ATP / reserva blanda al confirmar pedido de venta (≤ ATP; exceso = warning) | 3PL | Integration.Tests; ADR [0023](../adr/0023-alcance-completo-didactico.md) | [5](guias/05-trabajador.md) | Implementado |
| RF-INV-012 | Lotes, FEFO al entregar/picking y estados de stock (Available/Quality/Blocked/Damaged) | 3PL | `InventoryWorkflowTests` (FEFO + `SetStockStatus`) | [5](guias/05-trabajador.md) | Implementado |
| RF-VEN-011 | Devoluciones comerciales (`ReturnIn`/`ReturnOut`) como comandos de posting | 3PL | `RangoScopeTests` (ReturnIn), `InventoryWorkflowTests` (ReturnOut) | [5](guias/05-trabajador.md) | Implementado |
| RF-DOC-011 | Handling units SSCC y packing list PDF con assert de bytes PDF | 3PL | Integration.Tests (HU + packing PDF) | [5](guias/05-trabajador.md) | Implementado |
| RF-INF-010 | KPI OTIF proxy; visor auditoría API sin prueba dedicada de UI | 3PL | Integration.Tests (OTIF) | [4](guias/04-administrador-aplicacion.md), [23](guias/23-auditoria.md) | Parcial |
| RF-DOC-010 | Albarán/CMR/factura con plantilla PDF por apartados, sello/firma PNG configurable (emisor fiscal); Imprimir ≠ PDF en cliente | 3PL | Integration.Tests | [14](guias/14-roles-permisos.md), [4](guias/04-administrador-aplicacion.md) | Cumple |
| RF-DOC-012 | Presupuestos (Quote): CRUD, PDF/Imprimir con plantilla comercial compartida, aceptar → pedido de venta | 3PL | QuotesView + API quotes | [5](guias/05-trabajador.md) | Implementado |
| RF-FAC-001 | Factura fiscal ES (RD 1619/2012): series, rectificativas idempotentes, cadena SIF/QR Verifactu LocalChain | 3PL | `RangoScopeTests` | [30](guias/30-puestos-3pl.md), [14](guias/14-roles-permisos.md), [23](guias/23-auditoria.md) | Implementado |
| RF-CAT-010 | Baja de material con stock exige aprobación 4 ojos (segundo Coordinador/Admin) | 3PL | `RangoScopeTests` | [14](guias/14-roles-permisos.md), [23](guias/23-auditoria.md) | Implementado |
| RF-POR-010 | Portal Cliente: consultar y solicitar pedidos/facturas propias (RLS) | 3PL | `SecurityGapTests` + Portal Orders UI | [6](guias/06-cliente-externo.md) | Implementado |
| RF-LAB-001 | Apéndice lab: Waves, UoM convert, MRP lite, RateCard, TMS (ADR 0024); wave release atómico | Lab | `AppendixLabTests` | Lab apéndice UI | Implementado |
| RF-TEN-001 | Multiempresa `Company` + claim JWT `company_id` + API assign/`/me` | Ind | Domain/Integration | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-TEN-002 | Stock multi-dueño `OwnerCustomerId` en grano + posting/ATP house-default | Ind | Migration + posting | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-TEN-003 | Condiciones pago/crédito con bloqueo al confirmar venta (también sin company claim) | Ind | `InventoryWorkflowTests.Credit_*`, Domain `IndustrialPlatformTests` | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-TEN-004 | Tipos de cambio FX, conversión API y aplicación al emitir factura | Ind | `InventoryWorkflowTests.IssueInvoice_Applies_Fx_*`, Domain `IndustrialPlatformTests` | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-FAC-010 | Verifactu AEAT HTTP bridge lab (`Verifactu:Mode=AeatHttp`; sin payload SIF real) | Ind | AeatHttpVerifactuSubmitter | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-INT-001 | SAP OData configurable (`Sap:Enabled`) + outbox/mapeo + sync materiales + push DN + UI Integraciones; mock si desactivado | Ind | Integrations + WireMock/handler tests + [12](guias/12-integraciones-sap.md) | [12](guias/12-integraciones-sap.md) | Implementado |
| RF-RF-001 | RF scan resuelve HU/SSCC o Material; 404 honesto si no existe | Ind | `/rf/scan` | Lab apéndice | Parcial |
| RF-OPS-001 | SignalR OperationsHub: posting emite `stockChanged` tras mutación | Ind | Host hub + PostingService | [0025](../adr/0025-industrial-in.md) | Parcial |
| RF-OPS-002 | SMTP lab/staging (`Email:Mode=Smtp`; sin prueba E2E de entrega) | Ind | SmtpEmailSender | [16](guias/16-smtp.md) | Parcial |
| RF-REG-010 | Captcha PoW opt-in; portal resuelve challenge; sin prueba E2E CI | Ind | `ICaptchaVerifier` + Register.razor | [6](guias/06-cliente-externo.md) | Parcial |

## Requisitos no funcionales

| id | titulo | área | prueba | guia | estado |
|---|---|---|---|---|---|
| RNF-REN-001 | Lecturas paginadas p95 < 300 ms en red local | 9 | Escenario I | [29](guias/29-plataformas.md) | Pendiente |
| RNF-REC-001 | RPO hasta 24 h para todos los datos sin PITR | 2 | `SecurityGapTests` (backup fail-closed); escenario H | [8](guias/08-backups-recuperacion.md), [24](guias/24-recuperacion-desastres.md) | Pendiente |
| RNF-REC-002 | RTO < 1 h para BD de hasta 10 GB y su conjunto documental | 2 | Escenario H | [24](guias/24-recuperacion-desastres.md) | Pendiente |
| RNF-ACC-001 | Portal conforme a WCAG 2.2 AA | 7 | Escenario K / axe | [31](guias/31-wcag-portal.md) | Parcial |
| RNF-HA-001 | Multi-instancia Host + Postgres HA (runbook lab; `AAA_DISABLE_INSTANCE_LOCK`) | Ind | [ha-postgres](runbooks/ha-postgres.md) | [0025](../adr/0025-industrial-in.md) | Parcial |

## Requisitos de seguridad

| id | titulo | área | prueba | guia | estado |
|---|---|---|---|---|---|
| SEG-AUT-001 | Denegación por defecto: autenticación obligatoria y autorización por claim en cada endpoint | 1 | Integration.Tests (`SecurityGapTests`, auth endpoints) | [14](guias/14-roles-permisos.md) | Implementado |
| SEG-RLS-001 | Aislamiento por cliente con filtros EF Core y RLS PostgreSQL (`SET LOCAL` por transacción) | 3 | `SecurityGapTests`; ADR [0013](../adr/0013-rls-postgresql.md) | [9](guias/09-seguridad.md) | Implementado |
| SEG-AUD-001 | Cadena de auditoría con secuencia monotónica serializada y hash encadenado; falla cerrado | 2 | `InventoryWorkflowTests`; ADR [0019](../adr/0019-audit-chain-crypto-shred.md) | [23](guias/23-auditoria.md) | Implementado |
| SEG-EDT-001 | Lista negra absoluta del editor administrativo, incluso para Admin, por API evaluate (UI editor didáctico pendiente) | 6 | `AdminTableBlacklistTests`; ADR [0016](../adr/0016-admin-blacklist.md) | [4](guias/04-administrador-aplicacion.md) | Parcial |
| SEG-NET-001 | La API interna no se publica en internet; PostgreSQL solo en red privada | 1 | Despliegue / runbook (sin assert automatizado de topología) | [9](guias/09-seguridad.md), [17](guias/17-dominio-https.md), [seguridad-y-calidad.md](seguridad-y-calidad.md) | Parcial |
| SEG-SEC-001 | Ningún secreto en el repositorio; escaneo de secretos en CI | 0 | CI | [22](guias/22-colaboradores.md), [25](guias/25-rotacion-claves.md) | En curso |
| SEG-SUP-001 | Solo dependencias con licencia permisiva; SBOM y avisos de terceros por publicación | 0 | CI | [22](guias/22-colaboradores.md) | En curso |
| SEG-FAC-001 | Cadena SIF inmutable (hash por serie); anulación solo por rectificativa; emisión y config fiscal con permisos | 3PL | Integration.Tests | [9](guias/09-seguridad.md), ADR [0023](../adr/0023-alcance-completo-didactico.md) | Implementado |
| SEG-CAT-001 | Colusión en baja de material mitigada con 4 ojos (segundo aprobador) | 3PL | `RangoScopeTests` | [9](guias/09-seguridad.md), [23](guias/23-auditoria.md) | Implementado |
