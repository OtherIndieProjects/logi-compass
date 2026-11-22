namespace AAALogistics.Client.Views;

/// <summary>Textos de ayuda (ES) para iconos (i) de la oficina. Orientados a novatos de logística.</summary>
internal static class OfficeHelpTexts
{
    public static class Keys
    {
        public const string Home = "home";
        public const string Maestros = "maestros";
        public const string Ubicaciones = "ubicaciones";
        public const string Purchases = "purchases";
        public const string Sales = "sales";
        public const string Quotes = "quotes";
        public const string Prep = "prep";
        public const string Billing = "billing";
        public const string Inventory = "inventory";
        public const string Office = "office";
        public const string Lab = "lab";
        public const string WarehouseRf = "warehouse.rf";
        public const string Registration = "registration";
        public const string Reports = "reports";
        public const string Audit = "audit";
        public const string Accounts = "accounts";
        public const string Branding = "branding";
        public const string Sap = "sap";

        public const string StudioHub = "studio.hub";
        public const string StudioPricing = "studio.pricing";
        public const string StudioMm = "studio.mm";
        public const string StudioFinance = "studio.finance";
        public const string StudioPp = "studio.pp";
        public const string StudioQm = "studio.qm";
        public const string StudioOrg = "studio.org";
        public const string StudioWorkflow = "studio.workflow";
        public const string StudioIntHub = "studio.inthub";
        public const string StudioBi = "studio.bi";
        public const string StudioHr = "studio.hr";
        public const string StudioExt = "studio.ext";

        public const string MaestrosMaterials = "maestros.materials";
        public const string MaestrosUnits = "maestros.units";
        public const string MaestrosWarehouses = "maestros.warehouses";
        public const string MaestrosSuppliers = "maestros.suppliers";
        public const string MaestrosCustomers = "maestros.customers";

        public const string FieldExternalId = "field.externalId";
        public const string FieldRequiresLot = "field.requiresLot";
        public const string FieldUnit = "field.unit";
        public const string FieldUomConversion = "field.uomConversion";
        public const string FieldLeadTime = "field.leadTime";

        // P0 — operaciones diarias
        public const string FieldGoodsReceipt = "field.goodsReceipt";
        public const string FieldLotCode = "field.lotCode";
        public const string FieldLotExpiry = "field.lotExpiry";
        public const string FieldCancelOpenPo = "field.cancelOpenPo";
        public const string FieldClosePo = "field.closePo";
        public const string FieldConfirmSalesOrder = "field.confirmSalesOrder";
        public const string FieldCancelSalesOrder = "field.cancelSalesOrder";
        public const string FieldCloseSalesOrder = "field.closeSalesOrder";
        public const string FieldRejectSalesOrder = "field.rejectSalesOrder";
        public const string FieldSalesReturn = "field.salesReturn";
        public const string FieldDeliveryPdf = "field.deliveryPdf";
        public const string FieldPackingList = "field.packingList";
        public const string FieldCreateHuSscc = "field.createHuSscc";
        public const string FieldPostDelivery = "field.postDelivery";
        public const string FieldIncoterms = "field.incoterms";
        public const string FieldCustomsRef = "field.customsRef";
        public const string FieldPrepMode = "field.prepMode";
        public const string FieldJoinPrep = "field.joinPrep";
        public const string FieldLeavePrep = "field.leavePrep";
        public const string FieldMarkPrepared = "field.markPrepared";
        public const string FieldClosePrepOrder = "field.closePrepOrder";
        public const string FieldConfirmPick = "field.confirmPick";
        public const string FieldStockAdjustment = "field.stockAdjustment";
        public const string FieldWarehouseTransfer = "field.warehouseTransfer";
        public const string FieldReverseMovement = "field.reverseMovement";
        public const string FieldStockOpening = "field.stockOpening";
        public const string FieldSetMinQty = "field.setMinQty";
        public const string FieldReconcileStock = "field.reconcileStock";
        public const string FieldCreateCount = "field.createCount";
        public const string FieldLoadLocations = "field.loadLocations";
        public const string FieldStockStatus = "field.stockStatus";
        public const string FieldPostInventoryCount = "field.postInventoryCount";
        public const string FieldLotId = "field.lotId";
        public const string FieldCreateCarrier = "field.createCarrier";
        public const string FieldApproveDeletion = "field.approveDeletion";
        public const string FieldRejectDeletion = "field.rejectDeletion";
        public const string FieldCmrPdf = "field.cmrPdf";
        public const string FieldDeliveryNotePdf = "field.deliveryNotePdf";
        public const string FieldLocationKind = "field.locationKind";
        public const string FieldLocationParent = "field.locationParent";
        public const string FieldLocationTransfer = "field.locationTransfer";
        public const string FieldPutaway = "field.putaway";
        public const string FieldDefaultLocation = "field.defaultLocation";
        public const string FieldInvoiceSeries = "field.invoiceSeries";
        public const string FieldInvoiceTaxRate = "field.invoiceTaxRate";
        public const string FieldIssueInvoice = "field.issueInvoice";
        public const string FieldRectifyInvoice = "field.rectifyInvoice";
        public const string FieldIssuerProfile = "field.issuerProfile";

        // Integration hub
        public const string FieldConnector = "field.connector";
        public const string FieldReceiveMessage = "field.receiveMessage";
        public const string FieldInbox = "field.inbox";
        public const string FieldOutbox = "field.outbox";
        public const string FieldPayload = "field.payload";
        public const string FieldExternalMapping = "field.externalMapping";

        // P1 Studio + Oficina
        public const string FieldTaxCode = "field.taxCode";
        public const string FieldPriceCondition = "field.priceCondition";
        public const string FieldCustomerGuid = "field.customerGuid";
        public const string FieldMaterialGuid = "field.materialGuid";
        public const string FieldRmaQty = "field.rmaQty";
        public const string FieldSupplierGuid = "field.supplierGuid";
        public const string FieldPoGuid = "field.poGuid";
        public const string FieldValuationMethod = "field.valuationMethod";
        public const string FieldThreeWayMatch = "field.threeWayMatch";
        public const string FieldSupplierInvoice = "field.supplierInvoice";
        public const string FieldGlAccount = "field.glAccount";
        public const string FieldJournalDoubleEntry = "field.journalDoubleEntry";
        public const string FieldJournalSource = "field.journalSource";
        public const string FieldQmLotHold = "field.qmLotHold";
        public const string FieldQmRelease = "field.qmRelease";
        public const string FieldOrgContext = "field.orgContext";
        public const string FieldBomParent = "field.bomParent";
        public const string FieldBomComponent = "field.bomComponent";
        public const string FieldMrpWarehouse = "field.mrpWarehouse";
        public const string FieldCmrVsDelivery = "field.cmrVsDelivery";

        // P2 Sistema / Maestros / Lab / Ext
        public const string FieldAccountRanges = "field.accountRanges";
        public const string FieldSuspendUser = "field.suspendUser";
        public const string FieldRegistrationApprove = "field.registrationApprove";
        public const string FieldMaterialName = "field.materialName";
        public const string FieldCatalogPrice = "field.catalogPrice";
        public const string FieldCurrency = "field.currency";
        public const string FieldWarehouseKind = "field.warehouseKind";
        public const string FieldDecimals = "field.decimals";
        public const string FieldTaxIdNif = "field.taxIdNif";
        public const string FieldSapEnqueue = "field.sapEnqueue";
        public const string FieldLabWave = "field.labWave";
        public const string FieldUserGuid = "field.userGuid";
        public const string FieldPluginGuid = "field.pluginGuid";
    }

    private static readonly Dictionary<string, (string Title, string Body)> Entries = new(StringComparer.Ordinal)
    {
        [Keys.Home] = (
            "Inicio",
            "Bandeja del día: cola de preparación (ALMACÉN), cierres de oficina y pedidos abiertos. " +
            "Preparación es trabajo de almacén (picking); Ventas es el pedido y la entrega fiscal. " +
            "Si no ve un menú a la izquierda, faltan rangos: SISTEMA → Cuentas. " +
            "Los accesos rápidos solo muestran lo que su rango permite."),

        [Keys.Maestros] = (
            "Maestros",
            "Aquí se dan de alta los datos base: unidades, materiales, almacenes, proveedores y clientes. " +
            "Orden típico: crear unidades → materiales → proveedores/clientes → almacenes; después ya puede comprar y vender. " +
            "IVA, descuentos y stock no se configuran en este menú."),

        [Keys.Ubicaciones] = (
            "Ubicaciones de almacén",
            "Use Nuevo almacén / Eliminar almacén (o Eliminar sobre la raíz). " +
            "Defina la jerarquía zona → pasillo → … → hueco con padre explícito (o raíz del almacén). " +
            "Colocar en hueco mueve stock desde la raíz hacia el nodo seleccionado; Traspasar mueve del padre a la hija."),

        [Keys.Purchases] = (
            "Pedidos de compra",
            "Cree un pedido de compra (PO) contra un proveedor y materiales maestros. " +
            "Cuando llegue la mercancía, selecciónelo y contabilice la recepción (GR): eso aumenta el stock. " +
            "La recepción no es la factura del proveedor; anular abierto no borra recepciones ya hechas."),

        [Keys.Sales] = (
            "Pedidos de venta",
            "Aquí vive el pedido comercial y la salida fiscal: cree → confirme → (picking en ALMACÉN → Preparación) → " +
            "Contabilizar entrega (DN / PGI): eso baja stock. " +
            "No recoja mercancía aquí: el picking es en Preparación o con escaneo en Almacén RF. " +
            "Incoterms y aduanas son opcionales; el PDF de albarán sale tras la entrega."),

        [Keys.Quotes] = (
            "Presupuestos",
            "Oferta comercial al cliente (no es factura). Cree con cliente, material, precio e IVA → " +
            "Marcar enviado → Aceptar crea el pedido de venta con esos precios. " +
            "PDF e Imprimir usan la plantilla comercial. Caduca al pasar la validez."),

        [Keys.Prep] = (
            "Preparación / picking (almacén)",
            "Menú ALMACÉN: cola de oficina para preparar pedidos ya confirmados (equivalente al picking / TO de almacén). " +
            "Elija modo Compartida o Exclusiva, entre, confirme líneas (FEFO) y marque preparado. " +
            "No baja stock: la entrega fiscal (DN/PGI) se hace después en Ventas → Contabilizar entrega. " +
            "Si trabaja con pistola/escaneo, use también ALMACÉN → Almacén RF para confirmar líneas en planta."),

        [Keys.Branding] = (
            "Apariencia / Marca",
            "Nombre comercial, logo (PNG/JPG) y color de acento del programa. "
            + "Se ve en cabecera, Inicio y PDFs. No sustituye Datos de mi empresa (fiscal) en Facturas."),
        [Keys.Billing] = (
            "Facturas",
            "Emisión de facturas fiscales ES (serie, IVA, PDF, sello/firma). " +
            "Configure primero «Datos de mi empresa» (razón, CIF, administrador, IBAN, pie). " +
            "La rectificativa corrige una factura ya emitida (abono); no sustituye al albarán de entrega."),

        [Keys.Inventory] = (
            "Inventario",
            "Consulte stock actual, movimientos y materiales bajo mínimo. " +
            "Ajustes usan cantidad firmada (+ suma, − resta); el estado (Disponible/Calidad/…) no es el hold de Calidad Studio. " +
            "Contabilizar conteo aplica el conteo creado al libro de stock."),

        [Keys.Office] = (
            "Oficina / CMR",
            "Cierre de oficina, transportistas, CMR y aprobación a cuatro ojos de bajas. " +
            "El CMR documenta el transporte internacional; el PDF de albarán es la entrega (DN). " +
            "No confunda Emitir CMR con Contabilizar entrega en Ventas."),

        [Keys.Lab] = (
            "Lab (avanzado)",
            "Funciones experimentales del apéndice (olas, UoM, MRP, tarifas, rutas). " +
            "El escaneo RF de producción está en ALMACÉN → Almacén RF. " +
            "Si una sección muestra 403, no tiene permiso para esa parte; el resto sigue usable."),

        [Keys.WarehouseRf] = (
            "Almacén RF",
            "Puesto de escaneo en planta (misma sesión oficina, rango Almacén): SSCC, putaway, confirmar picking y conteo. " +
            "No sustituye a Preparación: allí se abre la cola/sesión de oficina; aquí se confirma con escaneo. " +
            "Error típico: putaway sin hueco destino, o picking RF sin haber entrado antes a Preparación."),

        [Keys.Registration] = (
            "Registro",
            "Solicitudes de alta de usuarios pendientes de aprobación. " +
            "Aprobar o rechazar requiere el permiso de registro. " +
            "Tras aprobar, el trabajador aparece en Cuentas para asignar rangos de menú."),

        [Keys.Reports] = (
            "Informes",
            "Consultas de pedidos por estado, OTIF y exportación de stock a Excel. " +
            "No modifica datos: solo lectura y ficheros de salida. " +
            "Para el detalle operativo use Inventario o los menús de pedidos."),

        [Keys.Audit] = (
            "Auditoría",
            "Registro completo por área (accesos, maestros, compras, ventas, inventario, facturas, cuentas, Studio). " +
            "Filtre por fechas, usuario, tabla o acción; el detalle muestra valores anterior → nuevo en español. " +
            "Admin, Gerente, Finanzas y Calidad pueden consultar; verificar cadena y puntos de control son solo Admin. " +
            "Exporte CSV/JSON con los filtros activos."),

        [Keys.Accounts] = (
            "Cuentas",
            "Alta y ciclo de vida de trabajadores (suspender, reactivar, baja) y sus rangos SAP. " +
            "Al crear es obligatorio elegir ≥1 rango (Admin, Gerente, Compras, Ventas, Almacén, Finanzas, Calidad o Cliente). " +
            "Los rangos definen qué menús y acciones puede usar cada persona; permisos manuales solo Admin. " +
            "Suspender/baja pide motivo y quita el acceso sin borrar el historial."),

        [Keys.Sap] = (
            "Integraciones SAP",
            "Conexión opt-in a SAP OData: estado, sincronización de catálogo y outbox. " +
            "Encolar push de entrega envía un Id de DN hacia SAP; no es el Intercambio de datos del Studio. " +
            "Si SAP no está configurado en el servidor, esta pantalla lo indica."),

        [Keys.StudioHub] = (
            "Mapa del Studio",
            "AAA Logistics Studio agrupa los módulos avanzados; Logi Compass es la brújula del día a día. " +
            "Aquí ve qué fases (P0–P11) están operativas, incluido Intercambio de datos. " +
            "Doble clic abre un módulo habilitado; si está pendiente, espere a que se active en esta instalación."),

        [Keys.StudioPricing] = (
            "Precios / IVA / crédito / RMA",
            "Configure condiciones de precio, tipos de IVA de catálogo y límites de crédito fuera de Maestros. " +
            "Las RMA (devoluciones) usan los desplegables de cliente y material de Maestros. " +
            "El IVA de catálogo aquí no sustituye el % IVA al emitir factura en Facturas."),

        [Keys.StudioMm] = (
            "Compras MM avanzado",
            "Contratos marco y RFQ preparan compras; el 3-way match compara pedido, recepción y factura proveedor. " +
            "Valoración: 0 = precio estándar, 1 = media móvil. " +
            "Use Pedidos de compra para el PO diario; aquí está lo avanzado."),

        [Keys.StudioFinance] = (
            "Finanzas FI/CO lite",
            "Plan de cuentas España (siembra), centros de coste y asientos manuales con partida doble (≥2 líneas). " +
            "Debe y haber deben cuadrar; elija cuentas del desplegable (tras Sembrar CoA ES). " +
            "No hay alta de cuenta GL en UI: solo semilla y asientos."),

        [Keys.StudioPp] = (
            "Producción PP lite",
            "BOM (lista de materiales), rutas, orden de fabricación y MRP básico de necesidades. " +
            "Elija material padre, componente y almacén MRP en los desplegables (código + nombre). " +
            "No es un MES industrial completo; el Lab tiene otra vista MRP experimental."),

        [Keys.StudioQm] = (
            "Calidad QM",
            "Planes de inspección, holds de lote y certificados. " +
            "Al crear un hold, el stock de ese lote pasa a estado Calidad; al liberar vuelve a Disponible; al rechazar queda Bloqueado. " +
            "Elija material y lote en los desplegables."),

        [Keys.StudioOrg] = (
            "Sociedad / planta",
            "Registre plantas por sociedad, enlace almacenes y fije sociedad/planta por usuario. " +
            "Con contexto fijado, stock y listados sensibles se filtran; vacío = vista global (admin). " +
            "Cambiar el contexto requiere permiso de organización."),

        [Keys.StudioWorkflow] = (
            "Aprobaciones",
            "Reglas por tipo de documento e importe, cola pendiente y decisión con cuatro ojos. " +
            "Amplía la cola de Oficina para flujos configurables. " +
            "Quien aprueba no puede ser quien creó la petición."),

        [Keys.StudioIntHub] = (
            "Intercambio de datos",
            "Reciba y envíe mensajes con sistemas externos (conectores, bandeja de entrada/salida y mapeos). " +
            "Flujo típico: registrar conector → recibir mensaje → revisar bandeja → guardar mapeo Id externo↔interno. " +
            "Es distinto de Integraciones SAP en SISTEMA (OData/outbox hacia ERP)."),

        [Keys.StudioBi] = (
            "Reporting / BI",
            "Listados de stock valorado, antigüedad de cobros (aging) y OTIF. " +
            "Paneles de solo lectura; use Informes para exportar stock. " +
            "No modifica pedidos ni inventario."),

        [Keys.StudioHr] = (
            "RRHH lite",
            "Fichas de empleado ligadas a las Cuentas de acceso: listar, crear y actualizar. " +
            "Pegue el UserId (GUID) de la cuenta en Cuentas. " +
            "Use Cuentas para permisos; aquí solo datos laborales básicos (sin nómina)."),

        [Keys.StudioExt] = (
            "Extensibilidad",
            "Hooks y eventos de dominio, registro de plugins y transportes de configuración. " +
            "Probar plugin invoca el hook con el PluginId y parámetros JSON. " +
            "Pruebe primero en entorno de pruebas antes de producción."),

        [Keys.MaestrosMaterials] = (
            "Materiales",
            "Cada material es un artículo que compra, almacena o vende. " +
            "Debe tener unidad de medida; el precio de catálogo es orientativo. " +
            "Marque FEFO solo si gestiona caducidad por lote."),

        [Keys.MaestrosUnits] = (
            "Unidades de medida",
            "Códigos como UN, KG, L o PCS. Créelas antes que los materiales. " +
            "Los decimales indican si admite fracciones (p. ej. 3 para kg). " +
            "Las conversiones UoM relacionan dos unidades con un factor."),

        [Keys.MaestrosWarehouses] = (
            "Almacenes",
            "Almacén maestro (código y nombre). El tipo (Genérico / Campa / Nave) es clasificatorio. " +
            "Las ubicaciones físicas (huecos) se crean en el menú Ubicaciones. " +
            "Sin almacén no podrá recepcionar ni traspasar stock."),

        [Keys.MaestrosSuppliers] = (
            "Proveedores",
            "Datos fiscales y de contacto del proveedor para pedidos de compra. " +
            "El plazo en días ayuda a planificar recepciones esperadas. " +
            "El id externo enlaza con otro sistema (ERP, SAP) si lo usa."),

        [Keys.MaestrosCustomers] = (
            "Clientes",
            "Datos del cliente para pedidos de venta y facturación. " +
            "Necesita al menos nombre, NIF/CIF y ciudad. " +
            "El id externo es opcional para mapear con un sistema externo."),

        [Keys.FieldExternalId] = (
            "Id externo",
            "Código del mismo registro en otro sistema (ERP, SAP, Excel antiguo). " +
            "Es opcional: si no integra con nadie, déjelo vacío. " +
            "No sustituye al código interno de esta aplicación."),

        [Keys.FieldRequiresLot] = (
            "Requiere lote (caducidad)",
            "Si está marcado, las entradas y salidas exigen número de lote. " +
            "Se recomienda sacar primero lo que caduca antes (FEFO). " +
            "Actívelo solo en materiales con fecha de caducidad real."),

        [Keys.FieldUnit] = (
            "Unidad",
            "Unidad de medida base del material (debe existir en la pestaña Unidades). " +
            "Sin unidad no se puede guardar el material. " +
            "Si la lista está vacía, use Seed UN/KG/L/PCS o cree una unidad."),

        [Keys.FieldUomConversion] = (
            "Conversión de unidades",
            "Define cuántas unidades destino equivalen a 1 unidad origen (el factor). " +
            "Ejemplo: 1 CAJA = 12 UN → desde CAJA, hacia UN, factor 12. " +
            "Hace falta para comprar o vender en una unidad distinta a la base."),

        [Keys.FieldLeadTime] = (
            "Plazo proveedor (días)",
            "Días habituales desde el pedido hasta la recepción esperada. " +
            "Es informativo para planificación; no bloquea la recepción. " +
            "Déjelo vacío si aún no conoce el plazo."),

        [Keys.FieldGoodsReceipt] = (
            "Contabilizar recepción (GR)",
            "Registra la entrada de mercancía del pedido seleccionado y aumenta el stock. " +
            "No es la factura del proveedor; use Compras MM o Facturas según el caso. " +
            "Requiere permiso de recepción. Error típico: cantidad mayor que la abierta."),

        [Keys.FieldLotCode] = (
            "Lote",
            "Número de lote del proveedor o interno. Obligatorio si el material exige FEFO. " +
            "Si el material no lleva lote, déjelo vacío. " +
            "Error típico: olvidar el lote en un material con caducidad."),

        [Keys.FieldLotExpiry] = (
            "Caducidad del lote",
            "Fecha de caducidad en formato AAAA-MM-DD. " +
            "Sirve para sacar primero lo que caduca antes (FEFO). " +
            "Error típico: formato incorrecto o fecha en el pasado sin intención."),

        [Keys.FieldCancelOpenPo] = (
            "Cancelar / anular abierto",
            "Anula la cantidad aún pendiente del pedido (pide motivo). " +
            "No borra recepciones ya contabilizadas ni el historial. " +
            "Use Cerrar si el pedido terminó sin querer anular líneas."),

        [Keys.FieldClosePo] = (
            "Cerrar pedido de compra",
            "Cierra el pedido e impide más recepciones. " +
            "Úselo cuando ya no espera mercancía de ese PO. " +
            "Error típico: cerrar con cantidad abierta que aún iba a llegar."),

        [Keys.FieldConfirmSalesOrder] = (
            "Confirmar pedido de venta",
            "Pasa el pedido a Confirmado y lo deja listo para Preparación. " +
            "Sin confirmar no podrá entrar a picking. " +
            "Error típico: intentar preparar un pedido aún en borrador."),

        [Keys.FieldCancelSalesOrder] = (
            "Cancelar pedido abierto",
            "Anula el pedido seleccionado (pide motivo). No borra entregas ya hechas. " +
            "Úselo cuando el cliente desiste antes de cerrar. " +
            "Error típico: cancelar un pedido ya entregado o cerrado."),

        [Keys.FieldCloseSalesOrder] = (
            "Cerrar pedido de venta",
            "Cierra el pedido tras preparar/entregar lo previsto. Impide más entregas. " +
            "Distinto de Cancelar (que anula) y de Rechazar (que deniega el pedido). " +
            "Error típico: cerrar con mercancía aún por entregar."),

        [Keys.FieldRejectSalesOrder] = (
            "Rechazar pedido de venta",
            "Deniega el pedido seleccionado (no se preparará ni entregará). " +
            "No es lo mismo que Cancelar un pedido ya confirmado. " +
            "Error típico: rechazar sin haber seleccionado el pedido."),

        [Keys.FieldSalesReturn] = (
            "Devolución de entrada",
            "Registra una devolución de mercancía del cliente (vuelve stock). " +
            "No sustituye a una RMA de Studio ni a una factura rectificativa. " +
            "Error típico: devolver sin pedido/entrega previa."),

        [Keys.FieldDeliveryPdf] = (
            "PDF de entrega (albarán / DN)",
            "Descarga el PDF del albarán. Elija la entrega por número en el desplegable " +
            "(se rellena al Contabilizar entrega). " +
            "Error típico: pedir el PDF sin haber contabilizado antes."),

        [Keys.FieldPackingList] = (
            "Lista de empaque",
            "PDF con el detalle de bultos/contenido de la entrega. " +
            "Usa el mismo Id de entrega que el PDF de albarán. " +
            "Error típico: Id vacío o de otro documento."),

        [Keys.FieldCreateHuSscc] = (
            "Alta de bulto (SSCC)",
            "Crea una unidad de manipulación con código SSCC (Serial Shipping Container Code / código de bulto). " +
            "Útil para etiquetar cajas o palés del pedido. " +
            "Error típico: crear bulto sin pedido seleccionado."),

        [Keys.FieldPostDelivery] = (
            "Contabilizar entrega (DN / PGI)",
            "Salida fiscal: crea la entrega (DN) y da de baja stock (como PGI en SAP). " +
            "Hágalo cuando el picking esté listo en ALMACÉN → Preparación (Marcar preparado). " +
            "Requiere permiso de entrega. Error típico: entregar sin stock o sin haber preparado."),

        [Keys.FieldIncoterms] = (
            "Incoterms",
            "Condición comercial de entrega (p. ej. EXW, DAP, CIF). Opcional. " +
            "Aclara quién paga transporte y riesgo; no mueve stock por sí sola. " +
            "Si no aplica, déjelo vacío."),

        [Keys.FieldCustomsRef] = (
            "Referencia de aduanas",
            "Referencia aduanera o de despacho cuando el envío lo requiere. Opcional. " +
            "Es informativa para documentación; no contabiliza la entrega. " +
            "Déjela vacía en envíos nacionales sin aduana."),

        [Keys.FieldPrepMode] = (
            "Modo de preparación",
            "Compartida: varios operarios pueden trabajar el mismo pedido. " +
            "Exclusiva: solo usted entra; evita choques en el picking. " +
            "Error típico: modo exclusiva cuando otro compañero ya está dentro."),

        [Keys.FieldJoinPrep] = (
            "Entrar a preparación",
            "Une su sesión al pedido seleccionado con el modo elegido. " +
            "Después confirme líneas de recogida y marque preparado. " +
            "Error típico: entrar sin tener el pedido en Confirmado."),

        [Keys.FieldLeavePrep] = (
            "Salir de preparación",
            "Abandona la sesión de picking del pedido actual sin marcar preparado. " +
            "Otro operario (modo compartida) o usted más tarde puede volver a entrar. " +
            "Error típico: salir pensando que eso cierra el pedido (no lo cierra)."),

        [Keys.FieldMarkPrepared] = (
            "Marcar preparado",
            "Indica que el picking de almacén está listo. Después: VENTAS → Pedidos de venta → Contabilizar entrega. " +
            "No baja stock por sí solo (eso es la entrega / PGI). " +
            "Error típico: marcar preparado sin confirmar las líneas de recogida."),

        [Keys.FieldClosePrepOrder] = (
            "Cerrar pedido desde preparación",
            "Cierra el pedido de venta desde esta pantalla (mismo efecto que Cerrar en Ventas). " +
            "Úselo cuando ya no quede nada por preparar ni entregar. " +
            "Error típico: cerrar con líneas aún abiertas."),

        [Keys.FieldConfirmPick] = (
            "Confirmar recogida",
            "Confirma la línea de picking seleccionada (suele sugerir lote FEFO). " +
            "En oficina: aquí en Preparación; en planta con pistola: Almacén RF. " +
            "No baja stock fiscal: eso ocurre al Contabilizar entrega en Ventas. " +
            "Error típico: confirmar sin seleccionar línea en la rejilla de picks."),

        [Keys.FieldStockAdjustment] = (
            "Ajuste de stock",
            "Cantidad firmada: positiva suma, negativa resta. Indique motivo. " +
            "Úselo para correcciones de inventario, no para recepciones o entregas. " +
            "Error típico: poner cantidad positiva cuando quería restar."),

        [Keys.FieldWarehouseTransfer] = (
            "Traspasar entre almacenes",
            "Mueve cantidad del almacén origen al almacén destino (códigos distintos). " +
            "No crea mercancía nueva: solo cambia de almacén. " +
            "Error típico: origen = destino o cantidad mayor que el stock."),

        [Keys.FieldStockOpening] = (
            "Apertura de stock",
            "Carga inicial de cantidad para un material en un almacén (una vez). " +
            "Use cantidad positiva. No sustituye a una recepción de compra. " +
            "Error típico: apertura repetida o cantidad 0."),

        [Keys.FieldSetMinQty] = (
            "Fijar mínimo de stock",
            "Umbral bajo el cual el material aparece en «Bajo mínimo» e Inicio. " +
            "No mueve stock: solo define el aviso. " +
            "Error típico: olvidar seleccionar material y almacén."),

        [Keys.FieldReconcileStock] = (
            "Reconciliar libro de stock",
            "Compara el libro de movimientos con el stock actual y muestra diferencias. " +
            "No corrige solo: si hay descuadre, use Ajuste o Contabilizar conteo. " +
            "Error típico: esperar que Reconciliar arregle el stock automáticamente."),

        [Keys.FieldCreateCount] = (
            "Crear conteo",
            "Registra una foto del stock contado (aún no cambia el libro). " +
            "Después use Contabilizar conteo con el Id generado. " +
            "Error típico: crear conteo y olvidar contabilizarlo."),

        [Keys.FieldLoadLocations] = (
            "Cargar ubicaciones",
            "Rellena el desplegable de ubicaciones (huecos) del almacén seleccionado. " +
            "Necesario antes de Cambiar estado o trabajos por hueco. " +
            "Error típico: pulsar Cambiar estado sin haber cargado ubicaciones."),

        [Keys.FieldReverseMovement] = (
            "Revertir movimiento",
            "Deshace el movimiento elegido en el desplegable (tipo, material, cantidad y fecha). " +
            "Recargue Inventario si no ve el movimiento reciente. " +
            "Error típico: movimiento ya revertido o sin seleccionar."),

        [Keys.FieldStockStatus] = (
            "Estado de stock",
            "Disponible, Calidad, Bloqueado o Dañado en la ubicación elegida. " +
            "Bloqueado/Calidad impiden salidas operativas; no es lo mismo que el hold QM de Studio. " +
            "Error típico: cambiar estado sin elegir ubicación."),

        [Keys.FieldPostInventoryCount] = (
            "Contabilizar conteo",
            "Aplica al libro el conteo seleccionado en el desplegable. " +
            "Crear conteo solo registra la foto; Contabilizar es lo que ajusta stock. " +
            "Error típico: pulsar contabilizar sin haber creado el conteo."),

        [Keys.FieldLotId] = (
            "Lote",
            "Elija el lote en el desplegable (código y caducidad). " +
            "Vacío / «sin lote» = operación sin lote. " +
            "Error típico: dejar el desplegable en sin lote cuando el material exige lote."),

        [Keys.FieldLocationKind] = (
            "Tipo de ubicación",
            "Nivel en la jerarquía: Zona → Pasillo → … → Hueco. " +
            "Cree de arriba abajo; un hueco no puede ser padre de una zona. " +
            "Error típico: saltarse niveles o poner tipo incorrecto bajo el padre."),

        [Keys.FieldLocationParent] = (
            "Ubicación padre",
            "Nodo bajo el que cuelga la nueva ubicación. " +
            "«(Raíz)» cuelga al nivel de la raíz del almacén (código = código del almacén). " +
            "Error típico: elegir un padre de otro almacén o un hueco como padre de zona."),

        [Keys.FieldLocationTransfer] = (
            "Traspasar desde padre",
            "Mueve stock de la ubicación padre a la hija seleccionada en la rejilla. " +
            "Distinto de Colocar en hueco (que parte de la raíz del almacén). " +
            "Error típico: seleccionar un nodo sin padre con stock."),

        [Keys.FieldPutaway] = (
            "Colocar en hueco",
            "Mueve stock desde la raíz del almacén hacia el hueco seleccionado. " +
            "Es la colocación típica tras una recepción. " +
            "Error típico: intentar colocar sobre la raíz o sin material/cantidad."),

        [Keys.FieldDefaultLocation] = (
            "Raíz del almacén",
            "Nodo raíz creado con el almacén (código = código del almacén; nombre = nombre del almacén). " +
            "Ahí cae el stock al recepcionar; no se puede desactivar ni eliminar; es el origen de Colocar en hueco. " +
            "Error típico: intentar borrar la raíz o usarla como destino de colocación."),

        [Keys.FieldInvoiceSeries] = (
            "Serie de factura",
            "Serie fiscal de numeración (p. ej. A, B). " +
            "Cada serie lleva su propia secuencia; no la cambie a mitad de ejercicio sin criterio. " +
            "Error típico: serie vacía o distinta a la autorizada."),

        [Keys.FieldInvoiceTaxRate] = (
            "IVA % de la factura",
            "Porcentaje de IVA aplicado al emitir esta factura. " +
            "No es el catálogo de IVA del Studio Precios; es el tipo de este documento. " +
            "Error típico: poner 0,21 en lugar de 21."),

        [Keys.FieldIssueInvoice] = (
            "Emitir factura",
            "Crea una factura ordinaria al cliente seleccionado (serie, IVA, líneas). " +
            "No sustituye al albarán de entrega ni al CMR. " +
            "Error típico: emitir sin datos de emisor configurados."),

        [Keys.FieldRectifyInvoice] = (
            "Rectificativa / abono",
            "Corrige una factura ya emitida (seleccionada en la lista) con un motivo. " +
            "No anula el albarán ni la entrega de stock. " +
            "Error típico: rectificar sin seleccionar la factura original."),

        [Keys.FieldIssuerProfile] = (
            "Datos de mi empresa",
            "Perfil fiscal de la empresa que usa el programa: razón social, CIF/NIF, domicilio, " +
            "teléfono/email, nombre y cargo del administrador/firmante, IBAN, plazo, pie legal, " +
            "logo/sello/firma PNG. Un perfil activo por instalación. " +
            "Error típico: CIF vacío o imagen que no es PNG ≤512 KB."),

        [Keys.FieldConnector] = (
            "Conector",
            "Define quién habla con el sistema externo (código, nombre, tipo, versión). " +
            "Regístrelo antes de recibir mensajes en la bandeja. " +
            "Error típico: recibir mensajes sin conector o con código duplicado."),

        [Keys.FieldReceiveMessage] = (
            "Recibir mensaje",
            "Mete un mensaje en la bandeja de entrada para procesarlo. " +
            "Indique tipo, contenido (JSON) y, si aplica, el conector. " +
            "Error típico: JSON inválido o tipo de mensaje vacío."),

        [Keys.FieldInbox] = (
            "Bandeja de entrada",
            "Mensajes recibidos desde sistemas externos pendientes o ya procesados. " +
            "Revísela tras pulsar Recibir mensaje. " +
            "No es la outbox de Integraciones SAP."),

        [Keys.FieldOutbox] = (
            "Bandeja de salida",
            "Mensajes que esta aplicación envía o reintenta hacia fuera. " +
            "Consulte estado e intentos aquí. " +
            "Distinta del outbox SAP en SISTEMA → Integraciones SAP."),

        [Keys.FieldPayload] = (
            "Contenido del mensaje (JSON)",
            "Texto JSON que representa el mensaje a recibir. " +
            "Pegue un objeto válido, p. ej. {\"demo\":true}. " +
            "Error típico: comillas mal cerradas o campo vacío."),

        [Keys.FieldExternalMapping] = (
            "Mapeo externo ↔ interno",
            "Relaciona una clave del sistema externo con el Id interno (GUID) de esta app. " +
            "Evita crear duplicados al integrar. " +
            "Error típico: InternalId que no existe o clave externa vacía."),

        [Keys.FieldTaxCode] = (
            "Código / tipo IVA (catálogo)",
            "Alta de tipos de IVA en el catálogo Studio (código, nombre, %). " +
            "No es el % que escribe al emitir factura en Facturas. " +
            "Error típico: confundir ambos IVA."),

        [Keys.FieldPriceCondition] = (
            "Condición de precio",
            "Regla de descuento o importe del catálogo de precios. " +
            "Afecta a condiciones Studio, no al precio maestro del material. " +
            "Error típico: código duplicado."),

        [Keys.FieldCustomerGuid] = (
            "Cliente",
            "Elija el cliente en el desplegable (nombre y NIF). " +
            "Los datos vienen de Maestros → Clientes. " +
            "Error típico: dejar el desplegable vacío."),

        [Keys.FieldMaterialGuid] = (
            "Material",
            "Elija el material en el desplegable (código y nombre). " +
            "Obligatorio en RMA, contratos, BOM y valoraciones. " +
            "Error típico: dejar el desplegable vacío."),

        [Keys.FieldRmaQty] = (
            "Cantidad línea RMA",
            "Cantidad a devolver en la línea de la RMA. Debe ser positiva. " +
            "Va ligada al material elegido en el desplegable de la misma fila. " +
            "Error típico: cantidad 0 o negativa."),

        [Keys.FieldSupplierGuid] = (
            "Proveedor",
            "Elija el proveedor en el desplegable (Maestros → Proveedores). " +
            "Necesario para contratos marco y factura proveedor. " +
            "Error típico: dejar el desplegable vacío."),

        [Keys.FieldPoGuid] = (
            "Pedido de compra",
            "Elija el pedido de compra (número) para 3-way o factura proveedor. " +
            "La lista sale de Pedidos de compra. " +
            "Error típico: elegir un pedido ya cerrado sin querer."),

        [Keys.FieldValuationMethod] = (
            "Método de valoración",
            "Elija Precio estándar o Media móvil en el desplegable. " +
            "El valor unitario se guarda según el método elegido. " +
            "Error típico: dejar el desplegable vacío."),

        [Keys.FieldThreeWayMatch] = (
            "Conciliación 3-way",
            "Compara importes de pedido, recepción (GR) y factura proveedor. " +
            "Elija el pedido de compra en el desplegable; no sustituye Contabilizar recepción. " +
            "Error típico: crear el match sin pedido válido."),

        [Keys.FieldSupplierInvoice] = (
            "Factura proveedor (stub)",
            "Registra un importe de factura de proveedor para el 3-way. " +
            "No es la factura fiscal ES de menú Facturas. " +
            "Error típico: importe ≤ 0 o proveedor vacío."),

        [Keys.FieldGlAccount] = (
            "Cuenta contable (GlAccountId)",
            "GUID de una cuenta del plan sembrado (Sembrar CoA ES). " +
            "Listar cuentas y copie el Id. " +
            "Error típico: inventar un GUID que no existe en el plan."),

        [Keys.FieldJournalDoubleEntry] = (
            "Partida doble del asiento",
            "Al menos dos líneas: una con Debe y otra con Haber que cuadren. " +
            "Si no cuadran, el servidor rechaza el asiento. " +
            "Error típico: solo una línea o debe ≠ haber."),

        [Keys.FieldJournalSource] = (
            "Origen del asiento",
            "Etiqueta del origen (p. ej. MANUAL). Informativa para auditoría. " +
            "No crea la cuenta contable. " +
            "Déjelo claro si mezclan orígenes automáticos y manuales."),

        [Keys.FieldQmLotHold] = (
            "Hold de lote (calidad)",
            "Marca informativa de calidad sobre el lote elegido en el desplegable (código). " +
            "No sustituye el estado Bloqueado/Calidad de Inventario. " +
            "Error típico: creer que el hold ya impide la venta."),

        [Keys.FieldQmRelease] = (
            "Liberar / rechazar hold",
            "Cierra el hold de calidad; requiere permiso de calidad. " +
            "Tras liberar, revise si el estado de stock en Inventario sigue bloqueado. " +
            "Error típico: liberar sin LotId o sin permiso."),

        [Keys.FieldOrgContext] = (
            "Contexto sociedad / planta",
            "Fija qué sociedad y planta ve el usuario (filtra stock y listados). " +
            "Vacío = alcance global (admin). Cambiar requiere permiso de organización. " +
            "Error típico: fijar planta de otra sociedad."),

        [Keys.FieldBomParent] = (
            "Material padre (BOM / OF)",
            "GUID del material acabado o de la orden de fabricación. " +
            "Es la cabecera de la lista de materiales. " +
            "Error típico: usar el componente como padre."),

        [Keys.FieldBomComponent] = (
            "Componente BOM",
            "GUID del material que forma parte del padre. " +
            "Debe existir en Maestros. " +
            "Error típico: mismo GUID que el padre."),

        [Keys.FieldMrpWarehouse] = (
            "Almacén MRP (opcional)",
            "Almacén sobre el que calcular necesidades. Vacío = criterio por defecto del servidor. " +
            "Distinto del MRP experimental del Lab. " +
            "Error típico: GUID de ubicación en lugar de almacén."),

        [Keys.FieldCmrVsDelivery] = (
            "CMR frente a albarán",
            "Emitir CMR crea el documento de transporte; el Id de albarán/entrega sirve para el PDF de DN. " +
            "No contabiliza la entrega ni baja stock. " +
            "Error típico: emitir CMR pensando que ya entregó la mercancía."),

        [Keys.FieldCreateCarrier] = (
            "Alta de transportista",
            "Da de alta la empresa que lleva la mercancía (código + nombre). " +
            "Luego elíjala al emitir el CMR. " +
            "Error típico: código vacío o duplicado."),

        [Keys.FieldApproveDeletion] = (
            "Aprobar baja de material",
            "Confirma (cuatro ojos) la solicitud de baja del material seleccionado. " +
            "Tras aprobar, el material queda fuera de uso operativo. " +
            "Error típico: aprobar sin seleccionar la fila de la rejilla."),

        [Keys.FieldRejectDeletion] = (
            "Rechazar baja de material",
            "Deniega la solicitud de baja; el material sigue activo. " +
            "Seleccione la fila pendiente antes de pulsar. " +
            "Error típico: rechazar sin fila seleccionada."),

        [Keys.FieldCmrPdf] = (
            "PDF del CMR",
            "Descarga el PDF del último CMR emitido en esta sesión. " +
            "Primero pulse Emitir CMR. " +
            "Error típico: pedir el PDF sin haber emitido el CMR."),

        [Keys.FieldDeliveryNotePdf] = (
            "PDF de albarán / entrega",
            "Descarga el PDF del albarán eligiendo la entrega por número (misma lista que en Ventas). " +
            "No es el CMR. " +
            "Error típico: pedir PDF sin entregas contabilizadas."),

        [Keys.FieldAccountRanges] = (
            "Rangos de cuenta",
            "Rangos SAP: Admin, Gerente, Compras, Ventas, Almacén, Finanzas, Calidad (multi) o Cliente (exclusivo). " +
            "Al crear la cuenta es obligatorio marcar al menos uno. " +
            "Error típico: crear sin rango o combinar Cliente con rangos internos."),

        [Keys.FieldSuspendUser] = (
            "Suspender / dar de baja",
            "Suspender bloquea el acceso temporalmente; baja es el cierre definitivo del ciclo. " +
            "Ambos piden motivo. Reactivar solo aplica a suspendidos. " +
            "Error típico: baja sin motivo o confundir con borrar el historial."),

        [Keys.FieldRegistrationApprove] = (
            "Aprobar registro",
            "Aprueba (o rechaza) la solicitud de alta (permiso de registro). " +
            "Después asigne rangos en Cuentas. " +
            "Error típico: aprobar y olvidar los rangos."),

        [Keys.FieldMaterialName] = (
            "Nombre del material",
            "Texto visible del material en listados y selectores (además del código). " +
            "Obligatorio al crear. " +
            "Error típico: dejar solo el código y olvidar el nombre."),

        [Keys.FieldCatalogPrice] = (
            "Precio de catálogo",
            "Precio orientativo del material en Maestros. " +
            "No es el precio de la línea del pedido ni la valoración MM. " +
            "Use 0 si aún no lo conoce."),

        [Keys.FieldCurrency] = (
            "Moneda",
            "Código ISO de moneda del precio (p. ej. EUR). " +
            "Debe coincidir con lo que use en pedidos. " +
            "Error típico: escribir «euro» en lugar de EUR."),

        [Keys.FieldWarehouseKind] = (
            "Tipo de almacén",
            "Clasificación informativa: Genérico, Campa (exterior) o Nave (cubierta). " +
            "No crea ubicaciones: eso se hace en Ubicaciones. " +
            "Elija Genérico si duda."),

        [Keys.FieldDecimals] = (
            "Decimales de unidad",
            "Cuántos decimales admite la unidad (0 = solo enteros; 3 típico en kg). " +
            "Afecta a validaciones de cantidad. " +
            "Error típico: 0 en una unidad que se fracciona."),

        [Keys.FieldTaxIdNif] = (
            "NIF / CIF",
            "Identificador fiscal del proveedor o cliente. Obligatorio en altas. " +
            "Se usa en facturación y documentos. " +
            "Error típico: dejarlo vacío o con espacios raros."),

        [Keys.FieldSapEnqueue] = (
            "Encolar push de entrega",
            "Envía el albarán/entrega elegido por número a la cola hacia SAP. " +
            "Requiere SAP configurado; no es Intercambio de datos del Studio. " +
            "Error típico: no hay entregas o SAP deshabilitado."),

        [Keys.FieldLabWave] = (
            "Ola de preparación (Lab)",
            "Agrupa pedidos de venta para preparar en oleada (experimental). " +
            "Elija el pedido en el desplegable y pulse Añadir a la ola. No sustituye Preparación diaria. " +
            "Error típico: crear ola sin haber añadido ningún pedido."),

        [Keys.FieldUserGuid] = (
            "Usuario",
            "Elija la cuenta en el desplegable (Cuentas). " +
            "Úselo en RRHH y en contexto sociedad/planta. " +
            "Error típico: dejar el desplegable vacío."),

        [Keys.FieldPluginGuid] = (
            "Plugin",
            "Elija el plugin registrado para Probar plugin. " +
            "Pulse Listar plugins si el desplegable está vacío. " +
            "Error típico: probar sin haber registrado ningún plugin."),
    };

    public static (string Title, string Body) Get(string key)
    {
        if (Entries.TryGetValue(key, out var entry))
        {
            return entry;
        }

        return ("Ayuda", "No hay texto de ayuda para esta sección.");
    }
}
