# Plan del producto — AAA Logistics Studio (Logi Compass)

Qué es el producto, cómo está armado y qué queda fuera de alcance.

## 1. Qué es

**AAA Logistics Studio** es un sistema de oficina logística de escritorio (MIT).  
**Logi Compass** es la brújula del día a día: maestros, compras, almacén, ventas, facturación ES, informes y módulos Studio avanzados.

| Pieza | Rol | Tecnología |
| --- | --- | --- |
| Servidor | Fuente de verdad (API + reglas + BD) | .NET 10, PostgreSQL |
| Cliente | Oficina interna | Avalonia (Windows) |
| Portal | Clientes externos (opcional) | Blazor, proceso aparte |

Principios:

1. El servidor manda: el cliente no tiene lógica de negocio ni de seguridad propia.
2. Seguro por defecto y mínimo privilegio.
3. Stock y auditoría son libros de solo adición (correcciones = operaciones explícitas).
4. Toda función de usuario tiene guía en español.
5. Decisiones de arquitectura relevantes viven en `docs/adr/`.

## 2. Arquitectura

Capas: `Domain` ← `Application` ← `Infrastructure` / `Api` ← `Host`.  
El cliente y el portal solo hablan HTTP con la API.

El **Host** abre dos listeners en el mismo proceso:

- **5088** — API interna (`/api/v1/internal`) + salud + Scalar/OpenAPI  
- **5089** — API de portal (`/api/v1/portal`) + salud  

El proyecto Blazor **Portal** es otro proceso y consume el 5089.

Detalle de listeners, capas y despliegue: [`arquitectura.md`](arquitectura.md).

## 3. Dominio funcional

**Incluido (núcleo):**

- Catálogo (materiales, almacenes, ubicaciones, partners)
- Compras → recepción; ventas → preparación → entrega
- Inventario (apertura, ajuste, traspaso, conteo, lotes/FEFO)
- Facturación ES (series, rectificativas, SIF/QR local Verifactu)
- Presupuestos, CMR, packing, PDF comerciales
- Cuentas, rangos/permisos, auditoría, backups
- Marca del producto (nombre/logo/acento) separada del emisor fiscal
- Studio P0–P11 (precios, MM, FI lite, PP, QM, org, workflow, BI, RRHH lite, extensibilidad)
- SAP OData opt-in (mock si está desactivado)

**Fuera de alcance (explícito):**

- Nómina completa
- Paridad feature-a-feature con SAP ECC/S4
- SAP RFC/IDoc productivo
- Envío Verifactu AEAT en producción (cadena local sí)
- Hardware RF propietario / multi-marca SaaS concurrente

## 4. Seguridad (resumen)

- Identity: usuario + contraseña; permisos por rangos (estilo ERP)
- Hash Argon2id; JWT en API interna
- RLS PostgreSQL para aislamiento de cliente en portal
- Cadena de auditoría con verificación; borrado criptográfico de datos personales cuando aplica
- Backups cifrados y restauración documentada
- API interna pensada para red de confianza (127.0.0.1 / VPN), no Internet abierto

Cierres de seguridad y calidad ya aplicados: [`seguridad-y-calidad.md`](seguridad-y-calidad.md).

## 5. Documentación de proyecto

| Documento | Uso |
| --- | --- |
| [`arquitectura.md`](arquitectura.md) | Capas, puertos, despliegue |
| [`requisitos.md`](requisitos.md) | Catálogo RF / RNF / SEG |
| [`matriz-cobertura.md`](matriz-cobertura.md) | Función ↔ guía |
| [`guias/`](guias/) | Manuales de usuario |
| [`../adr/`](../adr/) | Decisiones de arquitectura |
| [`../../CHANGELOG.md`](../../CHANGELOG.md) | Historial de versiones |
| [`api-guia.md`](api-guia.md) | API para integradores |
| [`crear-app-logi-compass.md`](crear-app-logi-compass.md) | Apps propias contra el servidor |

## 6. Cómo trabajar en el código

```bash
dotnet restore AAALogistics.slnx
dotnet build AAALogistics.slnx
dotnet test AAALogistics.slnx
```

- Identificadores en inglés; UI y docs de usuario en español.
- Tablas `snake_case`; mutación de stock solo por comandos de posting.
- Secretos fuera del repo (`user-secrets` / variables de entorno).
- PRs: ver [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md).

## 7. Empaquetado

- Instalación local típica: `Desktop\LogisticSolution` (Cliente + Servidor + Portal).
- Release pública Windows: asset `LogisticSolution-Windows-x64.zip` en GitHub Releases.
