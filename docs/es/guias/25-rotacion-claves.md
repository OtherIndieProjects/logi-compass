# Guía 25: Rotación de claves y secretos

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-SEC-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 40 min |

## Para qué sirve

JWT, backup.key, pii-master.

## Antes de empezar

Requisitos previos:

- [ ] Ventana mantenimiento

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Rote JWT (invalida sesiones).

2. Custodie claves offline.

3. Rote secretos SMTP/BD.

## Resultado esperado

Nuevas copias con claves actuales.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Rotación JWT automática: roadmap.

## Véase también

- [09 Seguridad](09-seguridad.md)
