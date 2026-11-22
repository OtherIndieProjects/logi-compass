# Guía 14: Rangos y permisos

| | |
|---|---|
| **Audiencia** | Admin / Gerente |
| **Versión del producto** | 3PL SAP ranges |
| **Última revisión** | 2026-11-18 |
| **Requisitos cubiertos** | SEG-AUT-001, RF-IDN-003, RF-IDN-005; nota RF-DOC-010 (PDF/sello) |
| **Permisos necesarios** | `User.Read`, `User.Create`, `User.ManageRanges` |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Asignar **rangos SAP multi-asignables**: Admin, Gerente, Compras, Ventas,
Almacén, Finanzas, Calidad. El rango **Cliente** es exclusivo del portal y no
se combina con rangos internos. Al **crear** una cuenta es **obligatorio**
elegir ≥1 rango (ya no hay default Asistente). Los permisos JWT son la
**unión** de los rangos del usuario. Los permisos manuales solo los cambia un
**Admin**.

Matriz completa: [rbac-sap-ranges.md](../../rbac-sap-ranges.md).

## Antes de empezar

- [ ] Sesión Admin o Gerente.
- [ ] Cliente de escritorio actualizado (LogisticSolution).

## Pasos

1. Menú **Cuentas**. Indique usuario, marque **al menos un rango** y cree.
2. Seleccione el usuario y ajuste rangos (p. ej. Ventas + Almacén). Guarde.
3. Compruebe que no puede marcar Cliente junto a un rango interno.
4. (Solo Admin) Ajuste permisos sueltos solo si necesita excepción.
5. El usuario debe cerrar sesión y volver a entrar: el menú muestra solo lo
   autorizado (secciones vacías ocultas).

## Resultado esperado

Cada rango ve solo sus menús; Admin/Gerente ven sistema amplio; Cliente solo
portal. Compras no ve Ubicaciones; Almacén no muta pedidos de compra/venta.

## Solución de problemas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| 403 tras cambiar rangos | Token antiguo | Volver a iniciar sesión |
| Error al crear sin rango | Rango obligatorio | Marcar ≥1 casilla |
| Error al combinar Cliente | Regla exclusiva | Quitar rangos internos o Cliente |
| No aparece Cuentas | Sin `User.Read` | Usar Admin/Gerente |
| Studio / sección desaparecida | Sin claim del módulo | Asignar rango adecuado |

## Documentos y sello (RF-DOC-010 Parcial)

Permisos `DeliveryNote.Manage` / `Cmr.Manage` / `Invoice.Print` habilitan PDF de albarán/CMR/factura. El sello PNG del emisor fiscal es configurable en perfil fiscal; cobertura de plantilla/sello sigue **Parcial**.

## Véase también

- [26 Cuentas](26-cuentas-sesiones.md)
- [6 Cliente externo](06-cliente-externo.md)
- [4 Admin aplicación](04-administrador-aplicacion.md)
- [Matriz RBAC SAP](../../rbac-sap-ranges.md)
