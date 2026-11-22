# Guía 09: Seguridad y buenas prácticas

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-AUT-001, SEG-RLS-001, SEG-NET-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 45 min |

## Para qué sirve

Red, roles y superficies.

## Antes de empezar

Requisitos previos:

- [ ] Guía 1

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. API interna solo red de confianza.

2. 5089 aislado por middleware de superficie.

3. BD como `aaa_app`.

4. Acceso = usuario + contraseña + roles/permisos (sin TOTP de app).

5. Revise sesiones y refresh (guía 26).

## Resultado esperado

Arranque sin `AAA-DB-ROLE-UNSAFE`.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Pentest externo recomendable antes de exponer el portal a Internet.

## Véase también

- [17 HTTPS](17-dominio-https.md)
- [18 Datos](18-proteccion-datos.md)
