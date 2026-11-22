# Guía 01: Instalación y puesta en marcha del servidor

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-22 |
| **Requisitos cubiertos** | RF-IDN-001, SEG-NET-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30–60 min |

## Para qué sirve

Instalar PostgreSQL, el rol `aaa_app` y el Host API con dos listeners (5088 interno, 5089 portal).

## Antes de empezar

Requisitos previos:

- [ ] SDK .NET 10
- [ ] PostgreSQL 16/17
- [ ] Permiso para crear roles y bases

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Copie `deploy/.env.example` a `deploy/.env` y levante Postgres: `docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d postgres`.

2. Exporte `ConnectionStrings__Default` con usuario `aaa_app` (nunca el superusuario de bootstrap).

3. Arranque el Host: `dotnet run --project src/AAALogistics.Host`.

4. Compruebe salud: `curl -sS http://127.0.0.1:5088/health/ready` y `curl -sS http://127.0.0.1:5089/health/live`.

5. Cree el administrador: `dotnet run --project src/AAALogistics.Host -- create-admin --user admin --password '…'` (acceso = usuario + contraseña + rangos; sin autenticador TOTP).

## Resultado esperado

El proceso escucha en ambos puertos y rechaza roles BD inseguros (`AAA-DB-ROLE-UNSAFE`).

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `AAA-DB-ROLE-UNSAFE` | Cadena con superusuario o BYPASSRLS | Use rol `aaa_app` |
| `address already in use` | Instancia duplicada | Detenga Host; revise `Data/aaa-host.lock` |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

HTTPS y dominio público: guía 17.

## Véase también

- [03 Administrador del sistema](03-administrador-sistema.md)
- [21 Ajustes del servidor](21-ajustes-servidor.md)
