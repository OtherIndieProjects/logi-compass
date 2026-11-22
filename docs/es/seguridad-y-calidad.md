# Seguridad y calidad — cierres aplicados

Resumen de endurecimientos ya incorporados al código (referencia de mantenimiento).

## Autenticación y sesiones

- Inicio de sesión con usuario y contraseña (sin TOTP de aplicación en el producto actual).
- Permisos por rangos (Admin, Ventas, Almacén, etc.); el menú del Cliente se oculta según permisos.
- Cerrar sesión invalida el sello de seguridad (JWT de acceso dejan de valer).
- Suspender / reactivar / dar de baja usuario exige motivo cuando corresponde.
- Clave de idempotencia en operaciones críticas con ámbito por usuario.

## Base de datos y privilegios

- Rol de aplicación distinto del propietario del esquema.
- Políticas RLS en tablas de cliente; aceptaciones legales sin agujeros `NULL` en políticas.
- Cadena de auditoría verificable; tokens de identidad fuera de la cadena o con valores redactados.
- Datos personales auditados pueden someterse a borrado criptográfico (crypto-shred) según flujo documentado en la guía 18.

## Inventario

- Apertura de stock de un solo uso por material/almacén: si ya hubo movimientos, se rechaza.
- Salidas sin stock negativo; preparación y entrega alineadas (pedido preparado antes de PGI).
- Ajustes, reversiones y cierres de cantidad abierta con motivo obligatorio.

## Facturación y marca

- Emisor fiscal (CIF + razón) obligatorio antes de emitir.
- SIF/QR local en PDF; envío AEAT productivo fuera de alcance.
- Marca del programa (nombre/logo/acento) en `/branding`, separada del emisor fiscal.

## Pruebas

```bash
dotnet test AAALogistics.slnx
```

Incluye pruebas de dominio, integración, seguridad/autorización y arquitectura (NetArchTest).

## Fuera de este documento

Mediciones formales de rendimiento (p95), RPO/RTO y pentest externo antes de publicar el portal en Internet: pendientes de campaña dedicada, no bloquean el núcleo de oficina en instalación local.
