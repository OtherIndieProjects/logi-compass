# Modelo de amenazas STRIDE

- **Estado:** Borrador vivo — revisar cuando cambie la topología de despliegue.
- **Fecha:** 2026-10-28
- **Alcance:** instancia única v1: portal en Internet, API interna por VPN y PostgreSQL privado. Integraciones externas opcionales se tratan aparte.
- **Plan:** [`../es/PLAN.md`](../es/PLAN.md) · [`../es/seguridad-y-calidad.md`](../es/seguridad-y-calidad.md)

## 1. Topología y fronteras de confianza

```text
            Internet (no confiable)
                    │
        ════════════╪════════════  FC1: Internet → DMZ
                    ▼
        Proxy inverso HTTPS (TLS ≥1.2, HSTS)
                    │
                    ▼
        Portal Blazor/BFF (proceso separado)
                    │ cookies de sesión del BFF; tokens solo en servidor
        ════════════╪════════════  FC2: Portal → Host API (listener portal)
                    ▼
  ┌──────────────── Host API (un proceso, dos listeners) ────────────────┐
  │  Listener portal (audiencia portal)   Listener interno (audiencia int.)│
  └────────────────────────────┬─────────────────────────▲───────────────┘
                               │                         │
        ═══════════════════════╪═══ FC4 ═════════════════╪═══ FC3: VPN/red de confianza → listener interno
                               ▼                         │
                 PostgreSQL (red privada)        Cliente de escritorio (Avalonia)
                               │
        ═══════════════════════╪═══ FC5: Servidor → almacenamiento de backups / checkpoints externos
                               ▼
                 Backups cifrados y puntos de control de auditoría
```

| Frontera | Descripción |
|---|---|
| FC1 | Internet hacia proxy y portal: única superficie pública |
| FC2 | Portal BFF hacia el listener portal del Host API |
| FC3 | Clientes de escritorio por VPN/red de confianza hacia el listener interno |
| FC4 | Host API hacia PostgreSQL con cuenta de mínimo privilegio |
| FC5 | Servidor hacia almacenamiento externo de backups y checkpoints |
| FC6 | Consola local y comandos de recuperación (acceso físico/SSH al servidor) |

## 2. Activos

| Activo | Por qué importa |
|---|---|
| Libro `MovimientoStock` y `Stock` | Integridad del inventario; base de toda decisión logística |
| Cadena `AuditLog` y checkpoints | No repudio y detección de manipulación |
| Credenciales, hashes, refresh tokens, claves de firma y Data Protection | Suplantación total si se filtran |
| Datos personales de clientes y contactos | RGPD; reputación |
| Documentos de registro subidos | Datos personales; posible vector de malware |
| Backups y su clave de cifrado | Contienen todo; la pérdida de la clave los hace irrecuperables |
| Disponibilidad del Host API | Operación logística diaria |

## 3. Análisis por elemento

S = Suplantación · T = Manipulación · R = Repudio · I = Divulgación · D = Denegación de servicio · E = Elevación de privilegios

### 3.1. Portal y proxy (FC1)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| S | Relleno de credenciales y fuerza bruta contra clientes | Lockout Identity fijo (5/15 min), límite por IP y cuenta, step-up TOTP en mutaciones internas (no en login), mensajes sin enumeración | RF-IDN-001 |
| T | CSRF, XSS e inyección en formularios | Antifalsificación, CSP estricta con nonces, codificación de salida, validación en servidor (S6) | — |
| R | Cliente niega haber creado o cancelado un pedido | Auditoría encadenada con usuario, IP y correlación | SEG-AUD-001 |
| I | IDOR: un cliente lee documentos de otro | Autorización por recurso, filtros EF Core y RLS | SEG-RLS-001 |
| I | Robo de tokens desde el navegador | BFF: el navegador solo tiene cookie de sesión `HttpOnly`, `Secure`, `SameSite`; tokens en servidor | — |
| D | Abuso del registro público o subida de ficheros grandes | Captcha/prueba de trabajo, honeypot, límites de tamaño y frecuencia | RF-REG-001 |
| E | Acceso a endpoints internos desde el portal | Audiencias distintas; listener interno no publicado | SEG-NET-001 |

### 3.2. Host API (FC2, FC3)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| S | Token del portal reutilizado contra el listener interno | Validación estricta de audiencia por listener; ES256 con rotación | SEG-NET-001 |
| T | Mutación de stock fuera de los cinco comandos | Pruebas de arquitectura; cuenta BD sin permisos innecesarios | RF-INV-002 |
| T | Reintentos que duplican contabilizaciones | `Idempotency-Key` persistida con índice único y hash de payload | RF-CON-001 |
| T | Edición cruda de tablas críticas por Admin | Lista negra absoluta del editor por UI y API | SEG-EDT-001 |
| R | Operación de negocio confirmada sin auditoría | Auditoría en la misma transacción; falla cerrado | SEG-AUD-001 |
| I | Fuga de datos personales o secretos en logs | Serilog con filtrado; nunca contraseñas, tokens ni datos innecesarios | — |
| D | Agotamiento por peticiones costosas o exportaciones masivas | Paginación obligatoria, límites de frecuencia y tamaño | — |
| E | Riesgo aceptado: ambas superficies comparten proceso | Audiencias, RLS, mínimo privilegio y pruebas; separar si el riesgo lo exige (ADR 0002) | — |

### 3.3. Cliente de escritorio (FC3)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| S | Robo de refresh token del equipo | Almacén seguro del SO (S8); rotación y detección de reutilización | RF-IDN-004 |
| T | Cliente modificado salta validaciones | Toda regla y autorización en servidor | SEG-AUT-001 |
| I | Exportación Excel con datos no autorizados o fórmulas maliciosas | Solo datos autorizados; neutralización de fórmulas | — |
| E | Trabajador obtiene permisos de Admin | Claims granulares, denegación por defecto, matriz de autorización completa | SEG-AUT-001 |

### 3.4. PostgreSQL (FC4)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| S | Acceso directo a la BD desde la red | Solo red privada; nunca expuesto a internet; credenciales fuera del repositorio | SEG-NET-001 |
| T | Modificación o borrado de `AuditLog` | Cuenta de aplicación sin `UPDATE`/`DELETE`; trigger; hash encadenado y `verify-audit` | SEG-AUD-001 |
| I | Fuga entre clientes por identidad residual en el pool | RLS con `SET LOCAL` por transacción (S3) | SEG-RLS-001 |
| D | Disco lleno o bloqueos prolongados | Comprobación de espacio, locks ordenados, alertas | — |
| E | Cuenta de aplicación con privilegios de propietario | Cuentas separadas para migración, aplicación y backup | — |

### 3.5. Backups y consola local (FC5, FC6)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| T | Restaurar un backup manipulado o incoherente | Manifiesto con hashes; restauración como unidad | RNF-REC-001 |
| R | Restauración que oculta un tramo de auditoría | Registro de restauraciones fuera de la BD; checkpoints externos | SEG-AUD-001 |
| I | Robo de backups | Cifrado con clave custodiada fuera del servidor de BD | — |
| D | Pérdida de la clave de backup | Custodia externa exigida por el asistente; simulacro | — |
| E | Uso indebido de `create-admin` o `reset-2fa` | Solo acceso local; auditado también fuera de la BD | — |

## 4. Supuestos

- Instancia única, sin alta disponibilidad.
- La VPN o red de confianza la gestiona quien instala; no forma parte del producto.
- El sistema operativo del servidor está mantenido y endurecido según la guía 9.

### 3.6. Facturación fiscal y Verifactu (ADR 0022)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| T | Manipulación o borrado de registros SIF / facturas | Cadena hash encadenada; sin DELETE; anulación solo rectificativa; auditoría | SEG-FAC-001 |
| T | Emisión fraudulenta de facturas | Solo Ventas/Gerente/Admin; series controladas; step-up en config emisor | SEG-FAC-001, SEG-AUT-001 |
| R | Negación de emisión | Hash + número de serie únicos; PDF con QR Verifactu | SEG-FAC-001 |
| I | Fuga de facturas entre clientes en portal | Filtro por `customer_id` del JWT | SEG-RLS-001, RF-POR-010 |
| E | Elevación a emitir/anular sin rango | Claims por rango (unión); denegación por defecto | SEG-AUT-001 |

### 3.7. WMS, preparación y baja de material (ADR 0022)

| | Amenaza | Mitigación prevista | Requisito |
|---|---|---|---|
| T | Dos operarios pisan el mismo picking | Locks Exclusive/Open + presencia en UI | RF-VEN-010 |
| T | Baja de material con stock sin control | Solicitud + segundo aprobador (4 ojos) + step-up | SEG-CAT-001 |
| E | Cliente con rangos internos | Validación Cliente exclusivo en `SetRangesAsync` | RF-IDN-005 |

## 5. Pendientes

- Diagrama C4 de contenedores y flujos de datos detallados.
- Revisar tras S3, S6, S7 y S8.
- Puntuar riesgo residual de cada amenaza y enlazar todas las filas con requisitos `SEG-`.
- Revisión externa antes de abrir el portal a clientes reales (plan / seguridad-y-calidad).
- Envío AEAT VerifactuSubmit cuando existan credenciales (hoy LocalChain).
