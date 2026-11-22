# Guía 04: Administrador de la aplicación

| | |
|---|---|
| **Audiencia** | Administrador de empresa |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-11-01 |
| **Requisitos cubiertos** | RF-IDN-003, RF-REG-003, SEG-EDT-001  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 60 min |

## Para qué sirve

Maestros, clientes, registro y permisos sin editar ledger.

## Antes de empezar

Requisitos previos:

- [ ] Rol Admin

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Mantenga catálogo (materiales, almacenes, unidades).

2. Apruebe o rechace registros portal con motivo.

3. Asigne varios usuarios Cliente a un mismo cliente.

4. Use solo tablas permitidas en editor admin.

5. Concilie stock (cap. informes). Consulte OTIF y el visor de auditoría ([guía 23](23-auditoria.md)).

## Resultado esperado

Cambios auditados; stock solo vía contabilización.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Tabla bloqueada` | Lista negra diseño del producto

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Ubicaciones WMS (Campa/Nave y LocationNode)

- `Warehouse.SiteKind` distingue Campa/Nave a nivel almacén.
- Dentro del almacén, el árbol `LocationNode` (Root con código = código del almacén + Zone/Aisle/Column/Row/Position/Height/Bin) sostiene el grano de stock.
- Tras un alta de almacén, el stock de apertura cae en la **raíz**. Use putaway (`PostPutaway`) o transferencia entre ubicaciones (`PostLocationTransfer`) para mover a bins operativos.
- FEFO / picking sugieren desde saldos `Available` por ubicación; estados Quality/Blocked/Damaged quedan fuera de ATP.

## Limitaciones

El núcleo cubre facturación fiscal ES (cadena local), reservas ATP, lotes/FEFO,
devoluciones, SSCC/packing y ubicaciones WMS. El apéndice Lab y algunas
capacidades industriales son opcionales según instalación. Fuera de alcance
habitual: SAP RFC productivo, envío SIF AEAT real y hardware RF dedicado.

## Véase también

- [14 Roles](14-roles-permisos.md)
- [05 Trabajador](05-trabajador.md)
