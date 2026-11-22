# Guía 10: Despliegue y migración

| | |
|---|---|
| **Audiencia** | Administración de sistema |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-22 |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 60 min |

## Para qué sirve

Publicar el Host, aplicar migraciones y poner en marcha el servicio en un entorno controlado.

## Antes de empezar

- [ ] Backup reciente (guía 08)
- [ ] Binarios publicados o carpeta LogisticSolution lista
- [ ] Secretos y cadena de BD fuera del repositorio

> Las contabilizaciones de stock no se editan a mano: use el comando de corrección adecuado.

## Pasos

1. Publique el Host (self-contained o framework-dependent) o use el ZIP de Release.
2. Configure secretos / `ConnectionStrings__Default` con el rol `aaa_app`.
3. Arranque una sola instancia (bloqueo en `Data/aaa-host.lock`).
4. Compruebe salud: `http://127.0.0.1:5088/health/ready`.
5. En Linux, opcional: `sudo ./deploy/install-linux.sh <publish>` (ver [`deploy/README.md`](../../../deploy/README.md)).

## Resultado esperado

Migraciones aplicadas, un solo Host escuchando, login de administrador operativo.

## Solución de problemas

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| Instancia duplicada | Segundo Host | Detenga el proceso extra; revise el lock |
| `AAA-DB-ROLE-UNSAFE` | Cadena con superusuario | Use `aaa_app` |

Si el problema persiste: [guía 13](13-faq-errores.md).

## Limitaciones

- El servicio systemd de ejemplo escucha el API interno (`5088`). El portal (`5089`) se añade por configuración.
- HTTPS público: [guía 17](17-dominio-https.md).

## Véase también

- [01 Instalación del servidor](01-instalacion-servidor.md)
- [deploy/README.md](../../../deploy/README.md)
