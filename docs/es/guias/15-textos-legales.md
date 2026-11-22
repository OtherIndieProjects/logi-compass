# Guía 15: Textos legales y modo estricto

| | |
|---|---|
| **Audiencia** | Administrador |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | RF-REG-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Aviso legal y privacidad en registro.

## Antes de empezar

Requisitos previos:

- [ ] Plantilla legal

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Adapte textos en `docs/es/legal/`.

2. Active `RequireLegalTexts`.

3. Verifique aceptación en approve.

## Resultado esperado

Registro bloqueado si falta texto.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Contenido legal del instalador.

## Véase también

- [06 Cliente externo](06-cliente-externo.md)
