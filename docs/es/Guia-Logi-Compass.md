# Guía AAA Logistics Studio — Manual para novatos (v1.10)

**Fecha:** 2026-11-22 (Europe/Madrid)  
**Producto:** AAA Logistics Studio (Logi Compass = brújula logística; cliente Avalonia + servidor Host)  
**Público:** quien **no** sabe logística ni el programa.  
**Fuente:** `docs/es/Guia-Logi-Compass.md` · **PDF:** `docs/Guia-Logi-Compass.pdf`  
**SAP vs menús:** `docs/es/sap-vs-logi-compass.md`

UI amigable: verbo+objeto, **(i)** en acciones críticas, desplegables código+nombre, sin TOTP.  
**Menú ALMACÉN** incluye Inventario, **Preparación** (picking oficina), Almacén RF (escaneo), Oficina/CMR y Lab.

---

## Índice

1. [Antes de empezar](#a-antes-de-empezar)
2. [Glosario](#b-glosario)
3. [Flujo SAP vs Logi (corto)](#b2-flujo-sap-vs-logi-corto)
4. [Barra superior](#c0-barra-superior)
5. [Menús uno a uno](#c-menús-uno-a-uno)
6. [Flujos de punta a punta](#d-flujos-de-punta-a-punta)
7. [Anexo](#e-anexo)

---

# A. Antes de empezar

## A.1 Qué es

**AAA Logistics Studio** es el producto. **Logi Compass** es el día a día: stock, compras, ventas, picking, entregas, facturas y Studio (precios, MM, FI, PP, QM…).

| Pieza | Arranque (instalación publicada) |
| --- | --- |
| **Servidor** | `Iniciar Servidor.bat` |
| **Cliente** | `Iniciar Cliente.bat` |

Carpeta típica en este PC: Escritorio → **LogisticSolution**. Credenciales en **LEEME.txt** (no se copian aquí).

## A.2 Arranque

1. Arranque **Servidor**, espere.  
2. Arranque **Cliente**.  
3. Login con usuario/contraseña (como SAP ERP: sin autenticador).  
4. En **Inicio** verá solo los menús de su **rango**. Si falta uno → SISTEMA → **Cuentas**.

Desde código: `dotnet run --project src/AAALogistics.Host` y cliente `AAALogistics.Client`.

## A.3 Primera vez (cero datos)

1. **Cuentas** (trabajadores)  
2. **Maestros** → Unidades (o Seed) → Materiales → Proveedores/Clientes → Almacenes  
3. **Ubicaciones** (hasta huecos)  
4. **Inventario** apertura *o* primera **Compra → recepción**  
5. **Ventas** (crear/confirmar) → **ALMACÉN → Preparación** → **Ventas → Contabilizar entrega** → Facturas / Oficina

---

# B. Glosario

| Término | En corto |
| --- | --- |
| **Material / UoM** | Artículo + unidad (UN, KG…). |
| **Almacén / hueco** | Sitio lógico vs sitio físico. |
| **PO / recepción (GR)** | Compra; la recepción **sube** stock. |
| **Pedido de venta** | Demanda del cliente (VENTAS). |
| **Preparación / picking** | Recoger mercancía **antes** de la salida fiscal (**ALMACÉN**). |
| **Almacén RF** | Escaneo en planta; confirma pick/putaway/conteo. |
| **Entrega (DN) / PGI** | Contabilizar entrega: crea albarán y **baja** stock. |
| **Factura / CMR** | Fiscal vs documento de transporte (CMR no baja stock). |
| **Lote / FEFO** | Partida con caducidad; sale primero lo que caduca antes. |
| **Rango / (i)** | Paquete de permisos / ayuda contextual. |

---

# B2. Flujo SAP vs Logi (corto)

Cadena mental SAP → menú nuestro:

| SAP | Logi Compass |
| --- | --- |
| Sales order | **VENTAS → Pedidos de venta** (crear/confirmar) |
| Outbound delivery | Nace al **Contabilizar entrega** (no hay menú DN aparte) |
| Warehouse pick | **ALMACÉN → Preparación** |
| RF confirm | **ALMACÉN → Almacén RF** |
| PGI | Misma **Contabilizar entrega** (baja stock) |
| Billing | **VENTAS → Facturas** |
| IM / stock | **ALMACÉN → Inventario** |

**Preparación** = cola/sesión de oficina. **Almacén RF** = pistola en planta. Ninguno baja stock; eso lo hace la entrega en Ventas.  
Detalle: `docs/es/sap-vs-logi-compass.md`.

---

# C0. Barra superior

**Actualizar** refresca el módulo. **Salir** cierra sesión. La ayuda **(i)** está en cada menú, no en la barra.

---

# C. Menús uno a uno

## STUDIO

| Menú | Para qué (1 línea) | Cómo empezar |
| --- | --- | --- |
| **Mapa Studio** | Índice P0–P11 | STUDIO → Mapa; doble clic en Operativo |
| **Precios / IVA / RMA** | IVA, condiciones, crédito, devoluciones | Listar/Nuevo; RMA elige cliente+material |
| **Compras MM** | Contratos, RFQ, 3-way, valoración | Desplegables de proveedor/material/PO |
| **Finanzas** | CoA ES, centros, asientos | Sembrar CoA ES → Contabilizar (debe=haber) |
| **Producción** | BOM, rutas, OF, MRP | Nuevo BOM (padre ≠ componente) |
| **Calidad** | Planes, hold informativo, certificados | Hold **no** bloquea venta; use estado stock |
| **Sociedad / planta** | Multi-empresa y filtro | Vincular almacén ↔ planta; fijar contexto |
| **Aprobaciones** | Reglas + cola 4 ojos | Aprobar/rechazar ≠ mismo usuario |
| **Intercambio** | Conectores/bandejas/mapeos | No sustituye Integraciones SAP |
| **BI** | Stock valorado, aging, OTIF | Solo lectura |
| **RRHH lite** | Empleado ↔ UserId | Sin nómina |
| **Extensibilidad** | Eventos, plugins, transportes | Probar plugin con JSON |

Sin permiso, el ítem **no aparece**. STUDIO vacío → no hay módulos habilitados para su rango.

---

## C.1 Inicio

Bandeja del día: **Preparación (almacén)**, cierre oficina/bajas, stock bajo mínimo, registro, ventas recientes.  
Accesos rápidos: Ventas/Facturas (comercial) e **Inventario / Preparación** (almacén).  
**(i)** explica menús ocultos por rango.

---

## C.2 Maestros

Orden: **Unidades → Materiales → Proveedores/Clientes → Almacenes**.  
Seed UN/KG/L/PCS si parte de cero. Material: Código + Nombre + Unidad.  
**Eliminar / Dar de baja** puede pedir 4 ojos en Oficina. IVA/stock **no** se configuran aquí.

---

## C.3 Ubicaciones

Jerarquía zona → … → **hueco**. **Colocar en hueco** mueve stock de la raíz al nodo. Necesita almacén maestro.

---

## C.4 Pedidos de compra (COMPRAS)

Crear PO → cuando llegue la mercancía **Contabilizar recepción** (sube stock). Opcional: colocar en hueco. Anular abierto no borra recepciones hechas.

---

## C.5 Pedidos de venta (VENTAS)

**Flujo:** Crear → **Confirmar** → picking en **ALMACÉN → Preparación** → volver → **Contabilizar entrega** (DN/PGI, baja stock) → elija la entrega por **número** en el desplegable → PDF / Imprimir albarán, lista empaque o SSCC.  
Incoterms y aduanas opcionales. Aquí **no** se recoge mercancía.

---

## C.5b Presupuestos (VENTAS)

Oferta al cliente (**no** es factura). Crear → Marcar enviado → **Aceptar → pedido** (crea SO con los precios del presupuesto). PDF e Imprimir usan la plantilla comercial. Caduca al pasar la validez.

---

## C.6 Preparación (ALMACÉN)

**Qué es.** Picking de oficina: cola de pedidos Confirmado / En preparación.  
**No** está bajo VENTAS.

1. ALMACÉN → **Preparación** → Actualizar.  
2. Modo **Compartida** o **Exclusiva**.  
3. Pedido → **Entrar** → **Confirmar recogida** (FEFO si aplica) → **Marcar preparado**.  
4. Entrega fiscal después en **Ventas → Contabilizar entrega**.

Escaneo en planta: **Almacén RF** (abra antes la sesión aquí si confirma picking).  
Permiso: `SalesOrder.Prepare` (rangos Ventas y Almacén lo tienen; menú bajo ALMACÉN).

---

## C.7 Inventario (ALMACÉN)

Stock, apertura, traspasos, ajustes (+/−), conteos, mínimo, estados.  
**No** es la cola de picking (eso es Preparación).

---

## C.7b Almacén RF (ALMACÉN)

Puesto compacto: SSCC, putaway, confirmar pick, conteo. Misma sesión oficina, rango Almacén.  
**No sustituye Preparación.** Lab queda como apéndice experimental.

---

## C.8 Facturas (VENTAS)

Configure emisor → Emitir / rectificativa / PDF. No sustituye el albarán de entrega.

---

## C.9 Oficina / CMR (ALMACÉN)

Cierre oficina, transportistas, **Emitir CMR**, PDF albarán (elegir entrega por número), bajas 4 ojos.  
CMR ≠ Contabilizar entrega.

---

## C.10–C.15 Sistema y Lab

| Menú | Notas |
| --- | --- |
| **Registro** | Aprobar/rechazar altas; luego rangos en Cuentas |
| **Informes** | Pedidos por estado, OTIF, Excel stock |
| **Auditoría** | Pestañas: Accesos, Maestros, Compras, **Ventas**, **Preparación**, Inventario, Facturas, Cuentas, Studio. Solo lectura; verificar cadena = Admin |
| **Cuentas** | Usuarios, rangos, permisos excepcionales |
| **Integraciones SAP** | Outbox / push DN (config servidor) |
| **Lab** | Experimental (olas…). RF diario → Almacén RF |

---

# D. Flujos de punta a punta

## Flujo 1 — Arranque en seco

Cuentas → Seed unidades → 1 material, proveedor, cliente, almacén → hueco → apertura stock.

## Flujo 2 — Compra

PO → **Contabilizar recepción** → (opcional) colocar en hueco. Stock sube.

## Flujo 3 — Venta (SAP-lógico)

1. **Ventas** → Crear → Confirmar.  
2. **ALMACÉN → Preparación** → Entrar → Confirmar recogida → Marcar preparado.  
3. (Opcional) **Almacén RF** si confirma en planta.  
4. **Ventas → Contabilizar entrega** → PDF.  
5. Factura / CMR si aplica.

## Flujo 4 — Corregir stock

Inventario → ajuste o crear/contabilizar conteo.

## Flujo 5 — Baja de material

Maestros → Eliminar/Dar de baja → segunda persona en Oficina aprueba.

## Flujo 6 — Hueco y putaway

Ubicaciones → crear hueco → Colocar en hueco (o RF putaway).

---

# E. Anexo

## E.1 Checklist

- [ ] Servidor + Cliente  
- [ ] Unidades y ≥1 material  
- [ ] Proveedor/cliente/almacén si compra/vende  
- [ ] Stock (apertura o recepción)  
- [ ] Emisor fiscal antes de facturar  

## E.2 Menú ↔ datos previos

| Menú | Necesita |
| --- | --- |
| Maestros / Unidades | Nada |
| Materiales | Unidades |
| Compras | Proveedor + material + almacén |
| Ventas | Cliente + material + stock |
| **Preparación (ALMACÉN)** | Pedido confirmado |
| Almacén RF | Sesión Prep si picking; hueco si putaway |
| Inventario apertura | Material + almacén |
| Facturas | Cliente + emisor |
| Oficina CMR | Entrega / transportista |

## E.3 Fuera de alcance de esta guía

Configurar PostgreSQL HA, certificados HTTPS de producción, ABAP/customizing SAP remoto, nómina.

## E.4 Ayuda (i)

Textos en `OfficeHelpTexts`. Cada menú de oficina y Studio tiene **(i)**; acciones críticas también.

---

*Fin de la guía v1.9 — Prep bajo ALMACÉN; flujo SAP→Logi explícito.*
