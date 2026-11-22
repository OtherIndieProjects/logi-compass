# Guía 26: Cuentas y sesiones

| | |
|---|---|
| **Audiencia** | Administrador y usuarios |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-18 |
| **Requisitos cubiertos** | RF-IDN-001–004  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 20 min |

## Para qué sirve

Login (usuario/contraseña), refresh, logout y control de acceso por roles/rangos.

## Antes de empezar

Requisitos previos:

- [ ] Acceso acorde al rol descrito.

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. 5088 interno; 5089 portal API.

2. Login con usuario y contraseña (sin autenticador TOTP).

3. Operaciones críticas: requieren el permiso/rango adecuado (y confirmación en UI si ya existía).

4. Refresh con rotación; reuso revoca familia.

5. Suspensión invalida JWT.

## Resultado esperado

Logout efectivo; críticas bloqueadas sin el permiso correspondiente.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-AUTH-0001` | Credenciales inválidas | Revisar usuario/clave |
| `AAA-AUTH-FORBIDDEN` | Sin permiso/rango | Asignar rangos en Cuentas |
| `AAA-AUTH-2FA-REMOVED` | Endpoint 2FA retirado | No enrolar autenticador; no es necesario |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Bloqueo por intentos fallidos = política Identity fija (5/15 min), no backoff progresivo. Alcance didáctico: la documentación de arquitectura interna. TOTP/step-up eliminados del producto (auth SAP-like).

## Véase también

- [14 Roles](14-roles-permisos.md)
