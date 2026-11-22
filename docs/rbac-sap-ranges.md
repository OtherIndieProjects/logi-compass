# Matriz RBAC — rangos SAP (strict)

Fuente de verdad de menús y claims por rango. Claves Identity sin acento (`Almacen`).  
Cliente es exclusivo (portal). Crear cuenta exige ≥1 rango (sin default Asistente).  
Overrides manuales: solo Admin.

## Rangos

| Clave | Etiqueta ES | Alcance |
|-------|-------------|--------|
| `Admin` | Administrador | Todo + Cuentas / Registro / Auditoría |
| `Gerente` | Gerente | Lectura amplia + cierres/aprobaciones; sin `User.Terminate` ni portal |
| `Compras` | Compras | Proveedores/materiales ligeros; PO; recepción |
| `Ventas` | Ventas | Clientes; pedidos venta; claim Prepare (menú Prep vive en ALMACÉN); facturas (sin mutar almacén) |
| `Almacen` | Almacén | Ubicaciones; inventario; **Preparación** + RF; oficina/CMR; lab; sin mutar compras/ventas create |
| `Finanzas` | Finanzas | Facturas + config IVA / Studio FI; consulta Auditoría; sin mutar stock |
| `Calidad` | Calidad | Studio QM + stock estado; consulta Auditoría |
| `Cliente` | Cliente (portal) | Solo portal |

### Migración legado → SAP

| Legado | Destino |
|-------|---------|
| `Admin` | `Admin` |
| `GerenteLogistica` | `Gerente` |
| `CoordinadorLogistica` | `Almacen` + `Compras` |
| `Administrativo` / `AsistenteLogistica` / `OperarioLogistica` / `Trabajador` | `Almacen` |
| `Ventas` | `Ventas` |
| `Cliente` | `Cliente` |

## Menú oficina × rango

| Menú / sección | Claim(s) puerta | Admin | Gerente | Compras | Ventas | Almacén | Finanzas | Calidad | Cliente |
|----------------|-----------------|:-----:|:-------:|:-------:|:------:|:-------:|:--------:|:-------:|:-------:|
| Inicio | (siempre) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | portal |
| Maestros | `Material.Manage` \| `Warehouse.Manage` \| `Supplier.Manage` \| `Customer.Manage` | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | — |
| Ubicaciones | `Location.Manage` | ✓ | ✓ | — | — | ✓ | — | — | — |
| Pedidos de compra | `PurchaseOrder.Create` | ✓ | ✓ | ✓ | — | — | — | — | — |
| Pedidos de venta | `SalesOrder.Create` \| `SalesOrder.Read` | ✓ | ✓ | — | ✓ | ✓* | — | — | — |
| Preparación (menú **ALMACÉN**) | `SalesOrder.Prepare` | ✓ | ✓ | — | ✓ | ✓ | — | — | — |
| Facturas | `Invoice.Issue` \| `Invoice.Print` | ✓ | ✓ | — | ✓ | ✓† | ✓ | — | — |
| Inventario | `Stock.Read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Almacén RF | `Stock.Read` \| `Stock.Transfer` \| `Stock.Adjust` \| `SalesOrder.Prepare` | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — |
| Oficina / CMR | `Cmr.Manage` \| `Carrier.Manage` \| `SalesOrder.Close` | ✓ | ✓ | — | — | ✓ | — | — | — |
| Lab | `Stock.Read` \| `Material.Manage` \| `SalesOrder.Prepare` | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | — |
| Registro | `Registration.Approve` \| `Registration.Reject` | ✓ | ✓ | — | — | — | — | — | — |
| Informes | `Report.Read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Auditoría | `Audit.Verify` | ✓ | ✓ | — | — | — | ✓ | ✓ | — |
| Cuentas | `User.Read` | ✓ | ✓ | — | — | — | — | — | — |
| Integraciones SAP | `Material.Manage` \| `Delivery.Post` | ✓ | ✓ | ✓ | — | ✓ | — | ✓ | — |

\*Almacén: `SalesOrder.Read` (consulta), no create/confirm mutadores.  
†Almacén: `Invoice.Print` solo.

Secciones vacías (STUDIO / MAESTROS / COMPRAS / VENTAS / ALMACÉN / SISTEMA) se ocultan si ningún ítem hijo es visible.  
Mapa Studio solo si el usuario tiene al menos un módulo Studio habilitado con claim.

## Studio × rango

| Módulo | AnyPermission | Admin | Gerente | Compras | Ventas | Almacén | Finanzas | Calidad |
|--------|---------------|:-----:|:-------:|:-------:|:------:|:-------:|:--------:|:-------:|
| Precios / IVA / RMA | Invoice.Issue \| Invoice.Config \| Customer.Manage \| Return.Manage | ✓ | ✓ | — | ✓ | — | ✓ | — |
| Compras MM | PurchaseOrder.Create \| Stock.Read \| Material.Manage \| Invoice.Config | ✓ | ✓ | ✓ | — | — | ✓‡ | — |
| Finanzas FI | Report.Read \| Invoice.Config | ✓ | ✓ | — | — | — | ✓ | — |
| Producción | Material.Manage \| PurchaseOrder.Create | ✓ | ✓ | ✓ | — | — | — | — |
| Calidad QM | Material.Manage \| Stock.Read \| Stock.Adjust | ✓ | ✓ | — | — | ✓ | — | ✓ |
| Sociedad / planta | Warehouse.Manage \| User.Read \| User.ManageRanges | ✓ | ✓ | — | — | ✓ | — | — |
| Aprobaciones | User.Read \| User.ManagePermissions \| PurchaseOrder.Create | ✓ | ✓ | ✓ | — | — | — | — |
| Intercambio | Material.Manage \| Delivery.Post | ✓ | ✓ | ✓ | — | ✓ | — | ✓ |
| BI | Report.Read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| RRHH | User.Read \| User.Create | ✓ | ✓ | — | — | — | — | — |
| Extensibilidad | Report.Read \| Material.Manage \| Admin.TableEdit | ✓ | ✓ | — | — | — | — | — |

‡Finanzas alcanza MM vía `Invoice.Config` (lectura/config; mutación PO sigue gated por API).

## Claims por rango (catálogo)

| Claim | Admin | Gerente | Compras | Ventas | Almacén | Finanzas | Calidad | Cliente |
|-------|:-----:|:-------:|:-------:|:------:|:-------:|:--------:|:-------:|:-------:|
| Material.Manage | ✓ | ✓ | ✓ | — | ✓ | — | ✓ | — |
| Material.Delete | ✓ | ✓ | — | — | — | — | — | — |
| Material.DeleteApprove | ✓ | ✓ | — | — | ✓ | — | — | — |
| Warehouse.Manage | ✓ | ✓ | — | — | ✓ | — | — | — |
| Location.Manage | ✓ | ✓ | — | — | ✓ | — | — | — |
| Supplier.Manage | ✓ | ✓ | ✓ | — | — | — | — | — |
| Customer.Manage | ✓ | ✓ | — | ✓ | — | — | — | — |
| Carrier.Manage | ✓ | ✓ | — | — | ✓ | — | — | — |
| PurchaseOrder.Create / CancelOpen / Close | ✓ | ✓ | ✓ | — | — | — | — | — |
| GoodsReceipt.Post | ✓ | ✓ | ✓ | — | — | — | — | — |
| SalesOrder.Create / Confirm / Cancel | ✓ | ✓ | — | ✓ | — | — | — | — |
| SalesOrder.Read | ✓ | ✓ | — | ✓ | ✓ | — | — | — |
| SalesOrder.Prepare | ✓ | ✓ | — | ✓ | ✓ | — | — | — |
| SalesOrder.Close | ✓ | ✓ | — | — | ✓ | — | — | — |
| SalesOrder.CreateOwn | ✓ | — | — | — | — | — | — | ✓ |
| Delivery.Post / DeliveryNote.Manage | ✓ | ✓ | — | ✓ | ✓ | — | — | — |
| Cmr.Manage | ✓ | ✓ | — | — | ✓ | — | — | — |
| Invoice.Issue / Rectify | ✓ | ✓ | — | ✓ | — | ✓ | — | — |
| Invoice.Print | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | ✓ |
| Invoice.Config | ✓ | ✓ | — | — | — | ✓ | — | — |
| Return.Manage | ✓ | ✓ | — | ✓ | — | — | — | — |
| Stock.Read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Stock.Transfer / Count | ✓ | ✓ | — | — | ✓ | — | — | — |
| Stock.Adjust / Reverse / OpeningImport | ✓ | ✓ | — | — | ✓ | — | ✓§ | — |
| Report.Read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Registration.* | ✓ | ✓ | — | — | — | — | — | — |
| User.Read / Create / Manage* / Suspend / Reactivate | ✓ | ✓ | — | — | — | — | — | — |
| User.Terminate | ✓ | — | — | — | — | — | — | — |
| Customer.Suspend | ✓ | ✓ | — | — | — | — | — | — |
| Admin.TableEdit | ✓ | ✓ | — | — | — | — | — | — |
| Audit.Verify | ✓ | ✓ | — | — | — | ✓ | ✓ | — |
| Backup.Manage | ✓ | ✓ | — | — | — | — | — | — |

§Calidad: `Stock.Adjust` para estado de lote/bloqueo; sin Transfer/Reverse/OpeningImport.
