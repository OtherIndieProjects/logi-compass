# Changelog

## [1.2.0] — 2026-11-22

### Oficina y facturación

- Multi-línea en presupuestos, pedidos de venta/compra y facturas; quitar línea del borrador.
- Factura desde pedido o albarán (DN→FI); líneas de factura en API y grid.
- PDF de recepción (GR), recibir/entregar abiertas, QR Verifactu en factura.
- Combos por número/etiqueta (sin pegar GUID) en oficina e inventario.
- Gate emisor (razón social + CIF) antes de emitir.
- Numeración documental en Cuentas; aging y stock valorado en Informes.

### API y documentación

- OpenAPI estático + Scalar en `http://127.0.0.1:5088/scalar`.
- Guías: `docs/es/api-guia.md`, `docs/es/crear-app-logi-compass.md`, `docs/Guia-API-Logi-Compass.pdf`.

### Marca del producto

- Panel **SISTEMA → Apariencia / Marca**: nombre, logo PNG/JPG y color de acento.
- API `GET/PUT /api/v1/internal/branding` (separado del perfil fiscal en Facturas).
- Cabecera del Cliente y PDFs comerciales usan la marca configurada.

### Repositorio y paquete

- Release GitHub con asset `LogisticSolution-Windows-x64.zip`.
- README con enlace de descarga y documentación de producto.

## [1.1.0-studio] — 2026-11-18

### Studio — base

- Marca de producto **AAA Logistics Studio** (Logi Compass = brújula logística dentro del studio) en `branding/brand.json`, Cliente (login/shell) y README.
- Menú **STUDIO** + **Mapa Studio** con `StudioModuleRegistry` (P0–P11) y ayuda (i) global sembrada para todos los módulos.
- Guía novato: sección **C.0 Mapa Studio**; identidad Studio en cabecera.
- Sin lógica SAP nueva en P0.

### Studio 1 — Precios / IVA / crédito / RMA

- Dominio + API `/studio` + UI con (i)/empty: tax codes, price conditions, credit summary, RMA.
- Guía C.0.1.

### Studio 2 — Compras MM avanzado

- Contratos marco, RFQ, 3-way match, valoración estándar/media, stub factura proveedor.
- Guía C.0.2.

### Studio 3 — Finanzas FI/CO lite (España)

- Plan de cuentas (seed ES), centros de coste, asientos, conciliación básica.
- Guía C.0.3.

### Studio 4 — Producción PP lite

- BOM, rutas, orden de fabricación, disparo MRP (Lab absorbido donde encaja).
- Guía C.0.4.

### Studio 5 — Calidad QM

- Planes de inspección, quality hold/release/reject, certificados.
- Guía C.0.5.

### Studio 6 — Multi sociedad / planta

- Plantas, enlace almacén, contexto de usuario (selector).
- Guía C.0.6.

### Studio 7 — Workflow aprobaciones

- Reglas, cola de peticiones, aprobar/rechazar (Oficina ampliada).
- Guía C.0.7.

### Studio 8 — Integration hub

- Conectores versionados, inbox, outbox existente expuesto.
- Guía C.0.8.

### Studio 9 — Reporting / BI

- Stock valorado, aging, OTIF passthrough.
- Guía C.0.9.

### Studio 10 — RRHH lite

- Fichas empleado ↔ user/cuentas; sin nómina completa.
- Guía C.0.10.

### Studio 11 — Extensibilidad

- Domain event log, plugins, transportes de configuración.
- Guía C.0.11.
- Migración EF `StudioModulesP1P11`.

## [1.0.0] — 2026-11-18

### Añadido

- Publicación del código limpio en repositorio GitHub privado `OtherIndieProjects/logi-compass`.
- README profesional en español (producto, audiencia, arquitectura, arranque Servidor+Cliente, mapa de menús, fuera de alcance, enlace a la guía PDF).
- Guía novato unificada v1.1 (100% menús): `docs/es/Guia-Logi-Compass.md` y PDF `docs/Guia-Logi-Compass.pdf`.

### Cubierto en la guía novato

- Antes de empezar, glosario al inicio, todos los menús de oficina con plantilla qué/para qué/datos/origen/pasos/después/errores/(i).
- Seis flujos punta a punta y anexo checklist / fuera de alcance.

### Fuera de alcance (explícito)

- Sin BOM.
- Sin IVA / descuentos en Maestros (IVA al emitir factura).
- Sin planta SAP / CAD de almacén inventada.

### Notas

- La guía PDF del proyecto queda en `docs/Guia-Logi-Compass.pdf` (sin rutas absolutas de usuario).
- No se incluyen secretos personales ni `bin/`/`obj/` en el repositorio.
