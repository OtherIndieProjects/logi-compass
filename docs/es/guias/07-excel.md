# Guía 07: Excel

| | |
|---|---|
| **Audiencia** | Exportación/importación |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | —  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Exportar hojas desde API interna.

## Antes de empezar

Requisitos previos:

- [ ] Token API interna

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Autentíquese en 5088.

2. Descargue endpoints `/api/v1/internal/exports/*.xlsx`.

3. No importe cambios de stock directos.

## Resultado esperado

Archivo Excel con cantidades numéricas.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `401` | Sesión caducada | Vuelva a entrar |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Stock solo por contabilización.

## Véase también

- [04 Administrador](04-administrador-aplicacion.md)
