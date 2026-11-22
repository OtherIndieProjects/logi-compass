# Guía 02: Instalación del cliente de escritorio

| | |
|---|---|
| **Audiencia** | Todos los usuarios internos |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | RF-IDN-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 15 min |

## Para qué sirve

Conectar el cliente Avalonia únicamente a la API interna (:5088).

## Antes de empezar

Requisitos previos:

- [ ] Credenciales válidas
- [ ] Acceso de red al servidor

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Instale o publique `AAALogistics.Client` para su sistema operativo.

2. Configure la URL base `http://<servidor>:5088` (no use 5089).

3. Inicie sesión con usuario y contraseña.

4. Compruebe que carga menús según su rol (permisos/rangos).

## Resultado esperado

Sesión activa en el cliente de escritorio.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `401` | Credenciales inválidas o sesión caducada | Guía 26 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Almacén seguro de tokens desktop: almacenamiento seguro de tokens en el cliente.

## Véase también

- [26 Cuentas y sesiones](26-cuentas-sesiones.md)
- [05 Trabajador](05-trabajador.md)
