# Guía 32: UI de oficina del cliente Avalonia

| | |
|---|---|
| **Audiencia** | Usuarios internos / formadores |
| **Última revisión** | 2026-11-13 |

## Auditoría UX (veredicto)

El menú **Maestros** es operable como maestro de datos logístico (SAP-lite): alta **y edición** de materiales, unidades, almacenes, proveedores y clientes; seed de unidades; conversiones UoM; **Dar de baja…** visible en Materiales; **Desactivar/Activar** en proveedores, clientes y materiales (PUT `IsActive`). El menú **Ubicaciones** cubre jerarquía completa (crear con padre, editar, desactivar, columnas Padre/Activo/Tipo ES).

## Dónde crear y editar maestros

1. Menú izquierdo → **Maestros**.
2. Elija la pestaña (Materiales, Unidades, Almacenes, Proveedores, Clientes).
3. Pulse **Nuevo…** o seleccione una fila y **Editar…**.
4. **Materiales:** **Eliminar / Dar de baja…** (baja formal `RequestMaterialDeletion`; con stock → 4 ojos en Oficina; sin step-up cae a desactivar). **Desactivar/Activar** = PUT `IsActive` inmediato, sin autenticador.
5. **Proveedores / clientes / materiales:** **Desactivar** / **Activar** (PUT `IsActive`; no borra documentos).
6. **Más acciones** (Excel / baja) solo en **Materiales**.
7. Unidades y almacenes no tienen flag Activo en el PUT actual; nodos físicos en **Ubicaciones**.

## Unidades y materiales

- Al abrir Maestros (con permiso de materiales) se asegura el seed **UN, KG, L, PCS** si faltan.
- Antes de **Nuevo material** se refrescan maestros; si no hay unidades, se muestra mensaje + atajo (pestaña Unidades / Seed). Guardar sin unidad no es silencioso.
- La rejilla de materiales muestra columna **Unidad**, precio, moneda, ExternalId, activo y lote.
- En **Unidades**: decimales, editar, botón **Seed UN/KG/L/PCS**, y panel de **conversiones UoM** (API appendix).

## Almacenes, proveedores y clientes

- Almacenes: columna **Tipo** (Generic / Campa / Nave) y edición. La estructura física (zonas/huecos) se gestiona en **Ubicaciones**.
- Proveedores / clientes: formularios con Dirección 2, Región, ExternalId; proveedores además **plazo (días)**; edición y **Desactivar/Activar**.

## Ubicaciones (jerarquía y stock)

1. Menú → **Ubicaciones de almacén**.
2. **Nuevo almacén** / **Eliminar almacén** (permiso `Warehouse.Manage`). Empty state: «Crea el primer almacén…».
3. Seleccione un almacén. La raíz se crea con el código/nombre del almacén (ya no existe nodo `DEFAULT`).
4. **Nueva ubicación:** código, nombre, tipo (Zona…Hueco) y combo **Ubicación padre** (o «Raíz»).
5. Jerarquía recomendada: **zona → pasillo → columna → fila → posición → altura → hueco**.
6. Rejilla: Código, Nombre, **Tipo** (ES), **Padre**, **Activo**.
7. **Editar…** renombra / cambia activo; **Desactivar** / **Activar** (la raíz no se desactiva).
8. **Eliminar** ubicación borra solo si no hay stock, hijas ni historial; si hay refs, el API exige **Desactivar**.
9. Stock: **Colocar en hueco (raíz → seleccionada)** y **Traspasar desde padre**.

## Ayuda (i) en la oficina

Cada menú de oficina muestra un icono **(i)** junto al título: abre un diálogo corto en español (título + 2–4 frases + **Cerrar**). El tooltip del botón es «Ayuda».

En **Maestros** hay además (i) por pestaña (Materiales, Unidades, Almacenes, Proveedores, Clientes) y ayuda de campo en formularios: ExternalId, Requiere lote (FEFO), Unidad, conversión UoM y plazo proveedor.

Los textos viven en `OfficeHelpTexts` (cliente Avalonia); no cambian la lógica de negocio.

## Fuera de este alcance (entregas siguientes)

Compras/Ventas/Preparación/Inventario/Facturas auditoría completa; stock mínimo; BOM; tarifas; IVA/descuentos.

## Véase también

- [04 Administrador de la aplicación](04-administrador-aplicacion.md)
- [05 Trabajador](05-trabajador.md)
