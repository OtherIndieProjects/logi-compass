# Guía 23: Registro de auditoría

| | |
|---|---|
| **Audiencia** | Administrador, Gerente, Finanzas, Calidad |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-20 |
| **Requisitos cubiertos** | SEG-AUD-001
| **Permisos necesarios** | `Audit.Verify` (consulta); verificar cadena / puntos de control: **Admin** |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Cadena append-only, consulta usable por área (accesos, maestros, compras, ventas/prep,
inventario, facturas, cuentas, Studio/sistema), exportación y puntos de control.

## Antes de empezar

Requisitos previos:

- [ ] Acceso acorde al rol descrito (`Audit.Verify`).
- [ ] Para integridad y checkpoints: cuenta Admin y Host local.

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.
> La cadena no se reescribe: el borrado criptográfico destruye claves PII sin tocar hashes.

## Pasos

1. En el cliente Avalonia, abra **Auditoría**. Elija la pestaña de área (p. ej. Accesos
   o Inventario), filtre por fechas/usuario/tabla/acción y pulse **Buscar**.

2. Seleccione una fila: el panel inferior muestra **Antes → Después** con etiquetas en
   español cuando el payload es JSON. Use **Exportar CSV** o **Exportar JSON** con los
   mismos filtros.

3. Integridad (solo Admin, o Host):
   - Cliente: **Verificar cadena**.
   - Host: `dotnet run --project src/AAALogistics.Host -- verify-audit`.

4. Punto de control (solo Admin; obligatorio antes de cualquier purga física):
   - Cliente: **Punto de control**, o
   - Host: `create-audit-checkpoint` / `audit-retention`.
   - Manifiestos en `Data/audit-checkpoints` junto a backups. El job de retención
     crea checkpoints periódicos y **bloquea** purgas sin checkpoint.

## Drill de aula (4 ojos + integridad)

1. Solicitar baja de un material con stock (usuario A) → queda pendiente.
2. Aprobar con **otro** usuario (cuatro ojos (segundo aprobador)).
3. En Auditoría → Maestros / Studio, localizar las entradas y revisar antes/después.
4. Ejecutar `verify-audit` y comprobar la secuencia monotónica.
5. Crear un punto de control y revisar `audit-retention`.

## Resultado esperado

Consulta filtrada con nombre de usuario; payloads descifrados cuando la clave existe;
`verify-audit` OK; checkpoint con secuencia/hash anclados.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| Menú Auditoría invisible | Sin `Audit.Verify` (p. ej. Almacén/Compras/Ventas) | Usar Admin/Gerente/Finanzas/Calidad |
| `Cadena inválida` | Manipulación/restore | Investigar y restaurar |
| `[cifrado — clave no disponible]` | Crypto-shred o clave ausente | Esperado tras shred; la cadena sigue válida |
| Purga bloqueada | Sin punto de control | Crear checkpoint primero |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Alcance: oficina logística, facturación ES y módulos Studio según su licencia de instalación.
Las filas de `audit_log_entries` son append-only (trigger + REVOKE); la retención no borra a ciegas.

## Véase también

- [18 Datos](18-proteccion-datos.md)
- la documentación de arquitectura interna
