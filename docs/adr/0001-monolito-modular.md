# ADR 0001: Monolito modular con arquitectura limpia

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Contexto:** arquitectura del Host y módulos de negocio.
- **Relacionado con:** ADR 0002 (dos listeners y portal separado, previsto), ADR 0009 (sin repositorio genérico, previsto)

## Contexto

El producto es un ERP logístico de instancia única, mantenido por un equipo pequeño y desplegable por terceros en Windows o Linux sin infraestructura compleja. Las reglas de negocio críticas (libro de stock, contabilización idempotente, cadena de auditoría) exigen transacciones ACID en una sola base de datos. Al mismo tiempo, los módulos (Identidad, Clientes y registro, Catálogo, Inventario, Compras, Ventas, Auditoría, Backups, Informes) deben mantenerse desacoplados para poder evolucionar y probarse por separado.

## Decisión

Construimos un **monolito modular con arquitectura limpia**:

- Capas con dependencias solo hacia dentro: `Domain` ← `Application` ← `Infrastructure` / `Integrations` / `Api` ← `Host`. `Contracts` contiene DTO y el contrato OpenAPI.
- Un único proceso **Host API** con los módulos de negocio, una base de datos PostgreSQL y transacciones locales.
- El **portal** Blazor/BFF y el **cliente** Avalonia son procesos separados que solo consumen la API.
- Los módulos se comunican mediante casos de uso de `Application` y eventos de dominio, nunca accediendo a las tablas de otro módulo.
- `Integrations` existe en la solución pero no se carga por defecto ni aporta migraciones a v1.0.

## Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Microservicios | Complejidad operativa (red, despliegue, consistencia distribuida) desproporcionada para una instancia única; rompería la atomicidad de la contabilización. |
| Monolito en capas sin módulos | Acoplamiento creciente entre áreas de negocio y límites no verificables. |
| Cliente con acceso directo a BD | Incompatible con el principio «el servidor es la única fuente de verdad» y con la seguridad. |

## Consecuencias

- **Positivas:** despliegue simple (un ejecutable más PostgreSQL), transacciones atómicas para stock y auditoría, límites claros y comprobables, posibilidad de extraer un módulo más adelante.
- **Negativas y riesgos aceptados:** escalado solo vertical; un fallo del proceso afecta a todos los módulos; las dos superficies API comparten proceso (ver ADR 0002).
- **Mitigaciones:** pruebas de arquitectura, audiencias separadas, RLS y mínimo privilegio.

## Verificación

- `AAALogistics.Architecture.Tests` comprueba la dirección de dependencias entre capas, la ausencia de referencias cruzadas entre módulos y que solo los cinco comandos de contabilización mutan `Stock`.
- CI falla si alguna prueba de arquitectura falla.

## Impacto en documentación

Guía 22 (colaboradores), diagramas C4 y modelo de amenazas.
