# Crear una app personalizada contra Logi Compass

**Objetivo:** montar una aplicación propia (escritorio, web o móvil) al estilo **GestionAlmacenKngloo**, adaptada a la empresa, **siempre hablando con el Servidor Logi Compass** — no reinventar stock, pedidos ni facturas.

**Fecha:** 2026-11-22 (Europe/Madrid)  
**API:** ver [`api-guia.md`](./api-guia.md) · Scalar `http://127.0.0.1:5088/scalar`

---

## 1. Idea

Logi Compass ya es el **sistema de registro** (ERP logístico ligero): maestros, stock, MM/SD/WM, facturas con identidad de empresa, PDF, RBAC.

Su app personalizada es la **cara** para un almacén, una marca o un flujo concreto:

- Pantallas solo de picking RF  
- Portal de un cliente grande  
- Tablet de muelle  
- Integración con básculas / impresoras de etiquetas  

Todo vía HTTP/JSON al Servidor. El Cliente Avalonia oficial puede seguir usándose en oficina.

---

## 2. Requisitos

1. Servidor Logi Compass en marcha (`Desktop\LogisticSolution\Servidor` o `Iniciar Servidor.bat`).  
2. Usuario interno con permisos del rango adecuado (Almacén, Ventas…).  
3. Base URL interna (defecto `http://127.0.0.1:5088`).  
4. Cliente HTTP + almacenamiento seguro del refresh token.

---

## 3. Esqueleto mínimo (cualquier lenguaje)

```text
1. POST /api/v1/internal/auth/login
2. Guardar accessToken (+ refresh)
3. GET /api/v1/internal/me  → permisos UI
4. GET /api/v1/internal/materials | /warehouses | /stock
5. Operar flujos (prep, entrega, GR, factura…)
6. Renovar token con /auth/refresh antes de caducar
```

Ejemplo C# (HttpClient):

```csharp
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", accessToken);
var stock = await client.GetFromJsonAsync<JsonElement>(
    "http://127.0.0.1:5088/api/v1/internal/stock");
```

Ejemplo TypeScript (fetch):

```ts
const res = await fetch(`${base}/api/v1/internal/stock`, {
  headers: { Authorization: `Bearer ${token}` },
});
```

---

## 4. Mapa funcional → endpoints (estilo almacén)

| Pantalla típica Kngloo | Endpoints Logi Compass |
|------------------------|-------------------------|
| Login | `/auth/login`, `/auth/refresh` |
| Artículos | `/materials`, `/units` |
| Existencias | `/stock`, `/stock/below-minimum`, PDF `/reports/stock/pdf` |
| Ubicaciones | árbol locations + putaway |
| Pedidos a servir | `/sales-orders`, prep `…/prep…` |
| Confirmar picking | prep picks confirm + RF `/rf/scan` |
| Albarán | `/deliveries/post`, list, PDF |
| Entradas proveedor | `/purchase-orders`, `/goods-receipts/post`, PDF GR |
| Inventario cíclico | `/stock/counts` |
| Etiquetas SSCC | `/handling-units` |
| Factura (si aplica) | `/issuer-fiscal`, `/invoices` |

No pegue GUIDs a mano en UI: liste recursos y muestre **números/códigos** (como hace el Cliente oficial).

---

## 5. Identidad de empresa

Las facturas y PDF comerciales usan el perfil **Datos de mi empresa** (`/issuer-fiscal`): razón social, CIF, dirección, IBAN, sello/firma/logo.  
Su app puede:

- Solo **consumir** PDF ya emitidos, o  
- Ofrecer pantalla de configuración que haga `PUT /issuer-fiscal`.

---

## 6. Buenas prácticas

1. **Un solo Servidor** por empresa/instalación; no duplicar lógica de stock.  
2. Respetar **RBAC**: ocultar botones sin permiso.  
3. Postings críticos: cabecera de idempotencia y permisos RBAC (sin TOTP).  
4. Preferir listados por **número de documento** (entregas, GR, facturas).  
5. UI en el idioma de la empresa; códigos de error `AAA-…` para soporte.  
6. Probar primero en Scalar (`/scalar`) antes de cablear pantallas.  
7. No commitear secretos; credenciales solo en el PC / almacén de secretos.

---

## 7. Alcance sugerido de una v1 personalizada

**Incluir:** login, stock, materiales, cola de preparación, confirmación de picks, entrega PDF, recepción PDF.  
**Dejar al Cliente oficial:** Studio, Lab, cuentas/RBAC admin, configuración fiscal avanzada.  
**Más adelante:** facturación embebida, portal B2B (puerto 5089), SignalR para colas en vivo.

---

## 8. Checklist de arranque

- [ ] Servidor responde `GET /health/live`  
- [ ] Scalar abre en `/scalar`  
- [ ] Login devuelve token  
- [ ] `GET /stock` con Bearer OK  
- [ ] Flujo prep → entrega en datos de prueba  
- [ ] PDF albarán/GR descarga  
