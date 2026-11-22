# Matriz de cobertura función–guía

Qué función del producto apunta a qué guía de usuario. Sirve para no dejar
flujos sin documentación.

Leyenda: **—** sin empezar · **Borrador** · **Revisada** · **Publicada**.

| Función | Requisitos | Guía | Sección | Ayuda contextual | Área | Estado guía |
|---|---|---|---|---|---|---|
| Inicio de sesión y bloqueo | RF-IDN-001 | 26 Cuentas y sesiones | Iniciar sesión | Pantalla de login | 1 | Borrador |
| 2FA y códigos de recuperación | RF-IDN-002 | 26 Cuentas y sesiones | Activar 2FA | Asistente 2FA | 1 | Borrador |
| Cerrar y revocar sesiones | RF-IDN-004 | 26 Cuentas y sesiones | Sesiones activas | Perfil / Administración de usuarios | 1 | Borrador |
| Roles y permisos | SEG-AUT-001 | 14 Roles y permisos | Conjuntos iniciales y claims | Editor de roles | 1 y 6 | Borrador |
| Usuarios de un cliente (1:N) | RF-IDN-003 | 4 Administrador de la aplicación | Clientes y usuarios | Ficha de cliente | 3 | Borrador |
| Libro de movimientos | RF-INV-001 | 4 / 5 | Cap. 4 Inventario | Consulta de movimientos | 3 | Borrador |
| Contabilizar recepción | RF-INV-002, RF-CON-001, RF-CON-002 | 5 Trabajador | Cap. 2 Compras y recepciones | Confirmación de contabilización | 4 | Borrador |
| Contabilizar entrega | RF-INV-002, RF-INV-003, RF-CON-001, RF-CON-002 | 5 Trabajador | Cap. 3 Ventas y entregas | Confirmación de contabilización | 4 | Borrador |
| Traspaso entre almacenes | RF-INV-002, RF-CON-001 | 5 Trabajador | Cap. 4 Inventario | Formulario de traspaso | 4 | Borrador |
| Ajuste, apertura y recuento | RF-INV-002, RF-CON-003 | 4 / 5 | Cap. 4 Inventario | Motivo obligatorio | 4 | Borrador |
| Reversión de movimiento | RF-INV-002, RF-CON-003 | 4 Administrador de la aplicación | Cap. 4 Inventario | Confirmación de reversión | 4 | Borrador |
| Conciliación de stock | RF-INV-004 | 4 Administrador de la aplicación | Cap. 6 Informes y conciliación | Estado del sistema | 4 | Borrador |
| Solicitud de registro (portal) | RF-REG-001, RF-REG-002 | 6 Cliente externo | Registrarse | Formulario de registro | 7 | Borrador |
| Aprobar o rechazar registro | RF-REG-003, RF-REG-004 | 4 Administrador de la aplicación | Solicitudes de registro | Bandeja de pendientes | 3 y 6 | Borrador |
| Editor administrativo | SEG-EDT-001 | 4 Administrador de la aplicación | Cap. 7 Permisos y lista negra | Vista previa antes/después | 6 | Borrador |
| Cadena de auditoría, consulta por área, export, checkpoints y `verify-audit` | SEG-AUD-001 | 23 Registro de auditoría | Verificar la cadena / consulta oficina | Menú Auditoría + Host | 2 | Borrador |
| Backups y restauración | RNF-REC-001, RNF-REC-002 | 8 Backups y recuperación | Backup y restauración | Estado del sistema | 2 | Borrador |
| Red y exposición | SEG-NET-001 | 9 Seguridad / 17 Dominio y HTTPS | Topología de red | — | 1 y 9 | Borrador |
| Contribuir al proyecto | SEG-SEC-001, SEG-SUP-001 | 22 Guía para colaboradores | Dependencias y secretos | — | 0 | Borrador |

## Pendiente de documentación

- Algunas guías temáticas siguen en borrador; la guía novato unificada y su PDF cubren el arranque.
- Rendimiento y accesibilidad: sin campaña de medición formal todavía.
