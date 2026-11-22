# Guía 12: Integraciones y SAP

| | |
|---|---|
| **Audiencia** | Administración técnica |
| **Versión del producto** | 1.1 (OData opt-in) |
| **Última revisión** | 2026-11-12 |
| **Requisitos cubiertos** | RF-INT-001 — ver documentación del producto |
| **Permisos necesarios** | `Material.Manage`, `Delivery.Post` (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Conector SAP **OData** opt-in: listar/sincronizar materiales hacia el catálogo interno, encolar push de entregas (DN) en outbox con reintentos, y consultar el estado desde la UI **Integraciones / SAP** del cliente Avalonia.

## Antes de empezar

- [ ] Host con sección `Sap` en `appsettings` (o variables `AAA_SAP_*`).
- [ ] Si `Sap:Enabled=true`, `BaseUrl` obligatorio (fail-closed al arrancar el cliente OData).
- [ ] Token interno con `Material.Manage` / `Delivery.Post`.
- [ ] Sistema SAP de pruebas o WireMock con rutas OData configuradas.

> **Importante:** no incluye RFC/IDoc. Orden recomendado: ficheros → OData → IDoc solo si hace falta.

## Configuración

```json
"Sap": {
  "Enabled": false,
  "BaseUrl": "https://sap.ejemplo:443",
  "UserName": "",
  "Password": "",
  "BearerToken": "",
  "TimeoutSeconds": 30,
  "MaterialsPath": "/sap/opu/odata/sap/API_PRODUCT_SRV/A_Product",
  "DeliveryPushPath": "/sap/opu/odata/sap/API_OUTBOUND_DELIVERY_SRV/A_OutbDeliveryHeader",
  "DefaultUnitCode": "UN"
}
```

Variables de entorno equivalentes: `AAA_SAP_ENABLED`, `AAA_SAP_BASE_URL`, `AAA_SAP_USERNAME`, `AAA_SAP_PASSWORD`, `AAA_SAP_BEARER_TOKEN`.

- `Enabled=false` → `MockSapODataClient` (lab, sin red).
- `Enabled=true` → `SapODataClient` real (Basic o Bearer).

## Pasos

1. Liste materiales: `GET /api/v1/internal/integrations/sap/materials`.
2. Sincronice al catálogo: `POST /api/v1/internal/integrations/sap/materials/sync` (crea/actualiza por `ExternalId`/código).
3. Empuje una DN: `POST /api/v1/internal/integrations/sap/deliveries/{deliveryId}/push` (escribe outbox; el worker reintenta).
4. Consulte outbox: `GET /api/v1/internal/integrations/sap/outbox`.
5. En el cliente: menú **Sistema → Integraciones**.

## Resultado esperado

JSON de materiales; sync con conteo creado/actualizado; push 202/204 con fila outbox; sin diferir mutaciones de stock (solo `PostingService`).

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| 401/403 | Permiso o audiencia | Token interno + claims |
| Arranque falla con Enabled | Falta BaseUrl | Completar `Sap:BaseUrl` |
| Outbox Pending/Failed | SAP caído o 4xx/5xx | Revisar `LastError`; reintento automático |
| 404 ruta | Prefijo incorrecto | Use `/api/v1/internal/integrations/sap/...` |

## Limitaciones

Sin IDoc/RFC, sin mapeo maestro completo de clientes/proveedores, sin AEAT. Credenciales nunca en el repositorio.

## Véase también

- [07 Excel](07-excel.md)
- Seguridad y calidad: [`../seguridad-y-calidad.md`](../seguridad-y-calidad.md)
- Runbook HA: [ha-postgres.md](../runbooks/ha-postgres.md)
