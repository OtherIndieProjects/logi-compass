# Guía 17: Dominio y HTTPS

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-NET-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 60 min |

## Para qué sirve

TLS en portal; API interna privada.

## Antes de empezar

Requisitos previos:

- [ ] Certificado TLS

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Proxy HTTPS al portal/5089.

2. No publique 5088.

3. Postgres solo red interna.

## Resultado esperado

Portal HTTPS; internal no expuesto.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Consulte la guía de instalación del servidor.

## Véase también

- [09 Seguridad](09-seguridad.md)
