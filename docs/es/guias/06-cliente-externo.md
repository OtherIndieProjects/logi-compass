# Guía 06: Cliente externo

| | |
|---|---|
| **Audiencia** | Usuarios portal |
| **Versión del producto** | 3PL (diseño del producto
| **Última revisión** | 2026-11-01 |
| **Requisitos cubiertos** | RF-REG-001, RF-REG-002, RF-REG-010 (Parcial), RF-POR-010  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 20 min |

## Para qué sirve

Registro, catálogo, **solicitar pedido**, consultar pedidos y facturas propias
(aislados por cliente). Ver [guía 30](30-puestos-3pl.md).

## Antes de empezar

Requisitos previos:

- [ ] Navegador actual

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Abra el portal Blazor (`dotnet run --project src/AAALogistics.Portal`).

2. Envíe registro sin rellenar honeypot `Website`. Con `Captcha:RequireProofOfWork=true`, el portal obtiene `/registration/pow-challenge` y resuelve el nonce antes de POST (RF-REG-010 Parcial lab).

3. Espere aprobación.

4. Inicie sesión en el portal (`/acceso`).

5. En **Pedidos**, solicite un pedido (almacén + material + cantidad).

6. Consulte **Facturas** propias cuando Ventas las emita.

7. No interprete stock como garantía.

## Resultado esperado

Solo datos de su empresa (RLS).

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-PORTAL-0008` | Registro denegado | Contacte admin |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Sin pago en línea. PoW es opt-in de lab (UI portal resuelve challenge); catálogo RF-REG-010 permanece Parcial hasta prueba E2E de challenge en CI.

## Véase también

- [15 Textos legales](15-textos-legales.md)
- [14 Roles](14-roles-permisos.md)
