# ADR 0019: Serialización de la cadena de auditoría y borrado criptográfico

- **Estado:** Aceptado
- **Fecha:** 2026-10-28 (aceptado 2026-10-31)
- **Spike relacionado:** S7 — incorporado ([carpeta](../../tools/spikes/s7-crypto-shred-audit/README.md), [estado de spikes](../es/spikes/README.md))
- **Referencias del plan:** `docs/es/PLAN.md`
- **Implementación:** `AuditChainService`, `PersonDataEncryptionService`,
  `CryptoShredPersonAsync`; evidencia en `InventoryWorkflowTests`
- **Relacionado con:** ADR 0007 (cadena de hashes de auditoría, previsto), ADR [0008](0008-backups-cifrados.md) (backups cifrados, Aceptado)

## Contexto

El plan exige un `AuditLog` de solo adición, encadenado por hashes y con
número de secuencia monotónico (§4.5). Los valores anterior y nuevo de cada
entrada pueden contener datos personales. El RGPD obliga a poder suprimirlos o
anonimizarlos (§4.13), pero reescribir una entrada rompería la cadena y la
detección de manipulación.

Además, dos escritores concurrentes no pueden leer la misma última entrada y
asignar la misma secuencia: la carrera en la cadena de auditoría figura en el
registro de riesgos (§23).

## Decisión (aceptada)

### Serialización de la cadena

La serialización es una decisión cerrada en §16.2 y aquí solo se concreta.
Cada transacción que escribe auditoría:

1. bloquea con `SELECT ... FOR UPDATE` la única fila de
   `CabeceraCadenaAuditoría` (o un mecanismo PostgreSQL equivalente);
2. calcula `secuencia = anterior + 1` y `hash = H(hash_anterior ‖ contenido)`;
3. inserta la entrada y actualiza la cabecera en la misma transacción que la
   operación de negocio.

Si no se obtiene el bloqueo o no se puede escribir la auditoría, la operación
de negocio se revierte (falla cerrado). La cuenta normal de BD no puede
modificar ni borrar `AuditLog`, y un trigger refuerza la prohibición.

### Borrado criptográfico (S7 incorporado)

1. Los datos personales de los valores auditados se cifran con una **clave por
   persona** (AES) envuelta por la clave maestra de instalación antes de
   construir la entrada.
2. El hash de la cadena se calcula sobre el **texto cifrado** (y el
   identificador de clave), nunca sobre el texto en claro.
3. Para suprimir a una persona se **destruye su clave** (`CryptoShredPersonAsync`).
   El texto cifrado sigue en la cadena, ilegible; `verify-audit` sigue
   validando secuencia, enlaces y hashes.

Residuos operativos (no reabren el ADR): retención de backups que aún
contengan la clave previa; UX de dato shreddeado en exportaciones.

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| Hash sobre el texto en claro y reescritura al suprimir | Rechazada: rompe la cadena o exige recalcularla, lo que anula la detección de manipulación. |
| No guardar datos personales en auditoría | Insuficiente: la auditoría debe registrar quién y qué cambió; se aplica minimización, pero no basta. |
| Tabla separada de datos personales referenciada por id | Se evaluará en S7 como variante; el borrado físico de esa tabla debe seguir siendo verificable y auditable. |
| Secuencia por `SEQUENCE` de PostgreSQL sin bloqueo | Rechazada para el encadenado: la secuencia no serializa el cálculo de `hash_anterior` y admite huecos. |

## Consecuencias

- **Positivas:** supresión RGPD compatible con una cadena de solo adición;
  orden total y verificable de las entradas.
- **Negativas y riesgos aceptados:** el bloqueo de cabecera serializa todas las
  escrituras auditadas; la custodia de claves añade una pieza crítica y
  perder una clave equivale a suprimir el dato.
- **Mitigaciones:** prueba concurrente obligatoria (§21 E), medición de
  rendimiento en S7 y procedimiento documentado de custodia y backups de claves.

## Verificación

Criterios de aceptación de S7 y de este ADR (detalle en
[`tools/spikes/s7-crypto-shred-audit/README.md`](../../tools/spikes/s7-crypto-shred-audit/README.md)):

- destruir la clave de una persona preserva la verificación completa de la
  cadena (puerta de S7, §22);
- al menos 100 escrituras concurrentes producen secuencias únicas y
  estrictamente crecientes, sin hashes rotos ni operaciones de negocio
  confirmadas sin su entrada (§21);
- el fallo del bloqueo o de la escritura de auditoría revierte la operación de
  negocio (§4.5, §21 E);
- `verify-audit` detecta manipulaciones (§4.5, §21 E).

## Impacto en documentación

Guía de administración (custodia de claves y `verify-audit`); spike S7;
[`docs/es/seguridad-y-calidad.md`](../es/seguridad-y-calidad.md).
