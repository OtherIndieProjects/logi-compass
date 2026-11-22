# Guía 13: FAQ y errores `AAA-<MÓDULO>-NNNN`

| | |
|---|---|
| **Audiencia** | Todos |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | —  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | referencia |

## Para qué sirve

Decodificar códigos estables.

## Antes de empezar

Requisitos previos:

- [ ] Acceso acorde al rol descrito.

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Localice `AAA-MOD-NNNN` en Problem Details.

2. Consulte tabla por módulo.

3. Escalada sin secretos en ticket.

## Resultado esperado

Acción clara por código.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-AUTH-2FA-REMOVED` / `AAA-AUTH-STEPUP-REMOVED` | Endpoints TOTP retirados | Use solo usuario/contraseña + permisos |
| `AAA-AUTH-FORBIDDEN` | Sin permiso/rango | Asigne rangos en Cuentas |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Lista evolutiva.

## Véase también

- [09 Seguridad](09-seguridad.md)
