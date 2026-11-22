# ADR 0022: Alcance 3PL — rangos, WMS, fiscal España

- **Estado:** Aceptado
- **Fecha:** 2026-11-01
- **Sustituye:** [ADR 0014](0014-alcance-v1.md) para el producto «Triple AAA Logistics Estudio» en modo ERP/3PL español.

## Contexto

El ADR 0014 cerró v1 sin facturación, ubicaciones internas, picking ni transportistas.
El producto debe operar como programa logístico por puestos (FP/3PL) con facturación
fiscal España, WMS anidado y rangos multi-asignables.

## Decisión

Entran en alcance:

1. **Rangos** multi-asignables (Admin, GerenteLogistica, CoordinadorLogistica,
   Administrativo, AsistenteLogistica, OperarioLogistica, Ventas) y **Cliente**
   exclusivo de portal.
2. **WMS** con ubicaciones anidadas (Campa / Nave) y stock por ubicación.
3. **Preparación de pedidos** con locks Exclusive/Open y estados EnPreparacion/Preparado.
4. **Albarán / CMR** imprimibles con sello PNG.
5. **Facturación fiscal ES** (RD 1619/2012) + SIF/Verifactu (cadena + QR; envío AEAT
   cuando haya credenciales).
6. **Baja de material con stock** con doble aprobación (4 ojos).

Siguen fuera (v2+ / industrial): MRP avanzado, multiempresa holding, SAP RFC, TMS de rutas,
oleadas robotizadas. El alcance didáctico ampliado (reservas, lotes, devoluciones, etc.)
está en [ADR 0023](0023-alcance-completo-didactico.md).

## Consecuencias

- Migraciones de esquema (roles, ubicaciones, prep sessions, billing, transportistas).
- Guías y requisitos deben reescribirse por rango.
- Amenazas nuevas: fraude fiscal, manipulación SIF, colusión en baja de material.
