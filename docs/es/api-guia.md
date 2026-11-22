# Guía de la API — Logi Compass (Servidor)

**Producto:** AAA Logistics Studio / Logi Compass  
**Versión API:** `v1` · superficie interna `/api/v1/internal`  
**Fecha:** 2026-11-22 (Europe/Madrid)  
**OpenAPI:** `http://127.0.0.1:5088/openapi/v1.json` (documento curado del núcleo; fuente `src/AAALogistics.Host/openapi/v1.json`)  
**UI interactiva (Scalar):** `http://127.0.0.1:5088/scalar`  
**Contratos C#:** `src/AAALogistics.Contracts/ApiRoutes.cs` (catálogo completo de rutas)  
**Marca (UI/PDF):** `GET/PUT /api/v1/internal/branding` (+ `/branding/logo`) — distinto de `issuer-fiscal`

Esta guía documenta la **API completa del Servidor** para clientes oficiales (Avalonia) y apps propias (estilo GestionAlmacenKngloo).  
Ver también: [`crear-app-logi-compass.md`](./crear-app-logi-compass.md).

---

## 1. Arquitectura

| Pieza | Rol |
|-------|-----|
| **Servidor** (`AAALogistics.Server`) | Minimal API + JWT + PostgreSQL. Dos listeners. |
| Puerto **interno** (defecto `5088`) | `/api/v1/internal/**`, `/health/**`, `/openapi/**`, `/scalar/**`, hub SignalR |
| Puerto **portal** (defecto `5089`) | Solo `/api/v1/portal/**` + health (B2B clientes) |
| Cliente Avalonia | App de oficina/RF oficial |
| App personalizada | Cualquier HTTP client contra el **mismo Servidor** |

Base URL típica en este PC:

```text
http://127.0.0.1:5088
```

---

## 2. Autenticación

### Login

`POST /api/v1/internal/auth/login`

```json
{
  "userName": "admin",
  "password": "••••••••"
}
```

Respuesta (simplificada): `accessToken`, `refreshToken`, permisos/roles.  
Usar en cabecera:

```http
Authorization: Bearer <accessToken>
```

### Refresh / logout

- `POST /api/v1/internal/auth/refresh`
- `POST /api/v1/internal/auth/logout`

### Autenticación (sin TOTP)

El producto usa **usuario + contraseña + rangos/permisos**. No hay autenticador TOTP ni step-up de aplicación.  
Los antiguos endpoints `/auth/2fa/*` y `/auth/step-up` responden con códigos `AAA-AUTH-2FA-REMOVED` / `AAA-AUTH-STEPUP-REMOVED`.

### Errores

Problemas en `application/problem+json` con extensión `code` (`AAA-…`) y `traceId`.

| HTTP | Significado |
|------|-------------|
| 401 | Sin token / inválido |
| 403 | Sin permiso |
| 404 | Recurso inexistente |
| 409 | Conflicto (p. ej. número duplicado) |
| 429 | Rate limit auth |

---

## 3. Permisos (RBAC)

Los endpoints usan políticas `AppPermissions.*` (p. ej. `Stock.Read`, `SalesOrder.Create`, `GoodsReceipt.Post`, `Invoice.Issue`).  
Tras login, consultar `GET /api/v1/internal/me` para la lista efectiva.  
Rangos de usuario (Admin/Ventas/Almacén/…) se administran en Cuentas; una app debe ocultar acciones sin permiso.

---

## 4. Mapa de dominios (API completa)

### 4.1 Auth e identidad

| Método | Ruta | Notas |
|--------|------|-------|
| POST | `/auth/login` | JWT |
| POST | `/auth/refresh` | |
| POST | `/auth/logout` | |
| POST | `/auth/change-password` | |
| GET | `/me` | Usuario + permisos |
| GET/POST | `/users` | Alta trabajadores (Admin) |

Prefijo real: `/api/v1/internal`.

### 4.2 Maestros

| Recurso | Rutas clave |
|---------|-------------|
| UdM | `GET/POST /units`, ensure-defaults |
| Materiales | `GET/POST/PUT /materials`, lots, deletion-requests |
| Almacenes | `GET/POST/PUT /warehouses` |
| Proveedores / clientes | `/suppliers`, `/customers` |
| Ubicaciones | árbol WM (`/locations`…) |
| Rangos numeración docs | `GET/PUT /document-number-ranges` |

### 4.3 Compras (MM)

| Flujo | Rutas |
|-------|-------|
| Pedido compra | `GET/POST /purchase-orders`, cancel-open, close, `{id}/pdf` |
| Recepción (GR) | `POST /goods-receipts/post`, `GET /goods-receipts`, `{id}/pdf` |

### 4.4 Ventas y presupuestos (SD)

| Flujo | Rutas |
|-------|-------|
| Presupuestos | `GET/POST /quotes`, send/accept/reject, `{id}/pdf` |
| Pedidos venta | `GET/POST /sales-orders`, confirm/cancel/close/reject, `{id}/pdf` |
| Preparación | `/sales-orders/{id}/prep…` (join/leave/picks/mark-prepared) |
| Entrega (DN/PGI) | `POST /deliveries/post`, `GET /deliveries`, `{id}/pdf`, packing-list |
| HU / SSCC | `/handling-units` |
| Devoluciones | `/sales-returns/post` |

### 4.5 Inventario / RF (IM+WM)

| Flujo | Rutas |
|-------|-------|
| Stock | `GET /stock`, below-minimum, reconcile |
| Movimientos | `GET /stock/movements`, reverse |
| Transfer / putaway / ajuste | `…/transfers/post`, putaway, adjustments |
| Conteos | `/stock/counts` + post |
| RF scan | `POST /rf/scan` |
| SignalR | hub `/hubs/operations` |

### 4.6 Facturación (FI) e identidad empresa

| Flujo | Rutas |
|-------|-------|
| Emisor (mi empresa) | `GET/PUT /issuer-fiscal` (+ sellos PNG) |
| Facturas | `GET/POST /invoices`, `{id}/pdf`, rectify |
| Series | por código en emisión |

### 4.7 Oficina / transporte

| Flujo | Rutas |
|-------|-------|
| Transportistas | `/carriers` |
| CMR | `POST/GET /cmr`, `{id}/pdf` |

### 4.8 Informes y auditoría

| Flujo | Rutas |
|-------|-------|
| Pedidos por estado / OTIF / stock PDF | `/reports/…` |
| Auditoría | `/audit/entries`, export, verify, checkpoints, retention |

### 4.9 Portal B2B

Puerto **5089**, prefijo `/api/v1/portal`: login, catálogo, pedidos cliente, registro.  
No mezclar tokens internal/portal (audiencias JWT distintas).

### 4.10 Studio / Lab / integraciones

Prefijo `/api/v1/internal/studio/…` y rutas de appendix/industriales: precios, RMA, BOM, FI journal, aging, SAP OData mock, etc. Útiles para ampliaciones; el núcleo almacén/oficina no las requiere.

---

## 5. Flujos típicos (curl)

Sustituya `TOKEN` y GUIDs.

### 5.1 Login + stock

```bash
curl -s -X POST http://127.0.0.1:5088/api/v1/internal/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"userName\":\"admin\",\"password\":\"SECRETO\"}"

curl -s http://127.0.0.1:5088/api/v1/internal/stock \
  -H "Authorization: Bearer TOKEN"
```

### 5.2 Pedido venta → prep → entrega

1. `POST /sales-orders` con `lines[]` (materialId, quantity)  
2. `POST /sales-orders/{id}/confirm`  
3. Prep: join + confirm picks + mark-prepared  
4. `POST /deliveries/post` (solo si estado Preparado / Parcial)  
5. `GET /deliveries/{id}/pdf`

### 5.3 Compra → recepción → PDF GR

1. `POST /purchase-orders`  
2. `POST /goods-receipts/post`  
3. `GET /goods-receipts?purchaseOrderId=…`  
4. `GET /goods-receipts/{id}/pdf`

### 5.4 Factura con datos de mi empresa

1. `PUT /issuer-fiscal` (razón social + CIF obligatorios)  
2. `POST /invoices` con líneas  
3. `GET /invoices/{id}/pdf` (incluye QR Verifactu LocalChain)

---

## 6. Idempotencia y posting

Operaciones de stock/entrega/factura críticas aceptan cabecera de idempotencia (el cliente oficial la envía). Reintentos seguros ante timeouts de red.

---

## 7. OpenAPI / Scalar

Con el Servidor en marcha (puerto interno):

1. Abrir **http://127.0.0.1:5088/scalar**  
2. JSON crudo: **http://127.0.0.1:5088/openapi/v1.json**  
3. Authorize → pegar Bearer token del login  
4. Probar endpoints

Documentación humana (esta guía) + OpenAPI generado desde Minimal API = fuente de verdad operativa.

---

## 8. Qué no es esta API

- No GraphQL / gRPC.  
- No sustituye el Cliente Avalonia: lo **complementa**.  
- Verifactu AEAT online no está; modo LocalChain + SIF/QR en PDF.  
- No publicar a GitHub.com sin orden explícita de Jose.

---

## 9. Referencias en el repo

| Archivo | Contenido |
|---------|-----------|
| `docs/es/api-guia.md` | Esta guía |
| `docs/es/crear-app-logi-compass.md` | Cómo montar una app tipo Kngloo |
| `docs/Guia-Logi-Compass.pdf` | Manual de usuario oficina |
| `src/AAALogistics.Contracts/ApiRoutes.cs` | Constantes de rutas |
| LogisticSolution Servidor | Publish local This PC |
