# ADR 0014: Alcance base cerrado de la versión 1.0

- **Estado:** Sustituido por [ADR 0022](0022-alcance-3pl-rangos-wms-fiscal.md) (2026-11-01)
- **Fecha:** 2026-10-28
- **Referencias del plan:** `docs/es/PLAN.md`

> **Nota:** El alcance operativo actual (3PL/fiscal/Studio) está en ADR 0022 y siguientes.
> Este ADR se conserva como histórico del cierre v1 «mini».

## Contexto

Un ERP tiende a crecer sin límite. Con un equipo pequeño, el crecimiento de alcance es el riesgo principal. Había que fijar qué entraba en v1.0 para planificar pruebas, guías y criterios de publicación.

## Decisión (histórico v1 mini)

Alcance v1.0 cerrado en su momento:

- **Dentro:** catálogo, inventario con libro de movimientos, apertura y recuento básico, compras y recepciones, ventas y entregas sin reserva, PDF de pedido de compra y albarán (no fiscales), registro de clientes con aprobación manual, identidad 1 Cliente : N Usuarios, auditoría y recuperación, editor administrativo estrecho, informes, Excel y experiencia básica.
- **Fuera entonces:** facturación, impuestos, MRP, ubicaciones avanzadas, picking, lotes, reservas, devoluciones, multiempresa, HA, móvil, SAP real, etc. (muchos de estos entraron después vía ADR 0022–0025).
- Plataformas cliente/servidor validadas con spikes S1, S5, S6 y S8.

Cualquier ampliación exige actualizar plan, requisitos, modelo de amenazas, pruebas, guías y un ADR nuevo.

## Consecuencias

- **Positivas:** planificación estable y textos que no prometían de más.
- **Negativas (en su día):** ciclos de facturación fuera; hoy cubiertos por ADRs posteriores.
- **Mitigaciones:** integraciones y versiones posteriores; etiqueta `fuera-de-alcance-v1` donde aún aplique.

## Verificación

- Revisión de PR: los cambios de funcionalidad enlazan un requisito existente en `docs/es/requisitos.md`.
- CI falla si un requisito implementado no tiene prueba o guía (§26).
