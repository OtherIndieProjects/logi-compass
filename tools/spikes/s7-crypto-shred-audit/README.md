# s7-crypto-shred-audit

Estado: incorporado al producto.
(`AuditChainService`, `CryptoShredPersonAsync`) y la puerta se prueba en
`InventoryWorkflowTests`: tras destruir la clave, `VerifyAsync` sigue siendo
válido y el texto cifrado ya no se descifra.

ADR: [0019 — Serialización de la cadena de auditoría y borrado criptográfico](../../../docs/adr/0019-audit-chain-crypto-shred.md) (**Aceptado**)

## Criterio

Borrado criptográfico y cadena serializada: destruir la clave preserva la
verificación de la cadena.

## Situación

El diseño de abajo se implementó en el producto  y la puerta de
verificación tras destruir la clave está cubierta por
`tests/AAALogistics.Integration.Tests/InventoryWorkflowTests.cs`. Esta carpeta
no contiene un prototipo desechable aparte.

## Diseño que se va a probar

1. Los datos personales de los valores auditados se cifran con una clave por
   persona.
2. La cadena encadena el **texto cifrado**: `hash = H(hash_anterior ‖ contenido)`,
   donde el contenido incluye el texto cifrado y el id de clave, no el texto en
   claro.
3. Suprimir a una persona = destruir su clave. El dato queda ilegible y la
   cadena no se reescribe.
4. La secuencia se asigna bajo `SELECT ... FOR UPDATE` sobre una única
   `CabeceraCadenaAuditoría`, en la misma transacción que la operación de
   negocio.

## Criterios de aceptación

Criterios de diseño:

1. Tras destruir la clave de una persona, `verify-audit` valida secuencia,
   enlaces y hashes de toda la cadena (puerta de S7).
2. Con al menos 100 escrituras concurrentes no hay secuencias duplicadas,
   hashes rotos ni operaciones de negocio confirmadas sin su entrada; las
   secuencias son únicas y estrictamente crecientes según el orden serializado.
3. Si falla el bloqueo, la asignación de secuencia o la escritura de
   auditoría, la operación de negocio se revierte (falla cerrado).
4. `verify-audit` detecta la manipulación de una entrada.
5. La cuenta normal de BD no puede modificar ni borrar `AuditLog`.

Derivados del diseño, a confirmar al cerrar el spike:

6. Tras destruir la clave, el dato personal no es recuperable por UI, API,
   exportación ni editor administrativo, y los datos de otras personas siguen
   legibles.
7. Queda documentado qué ocurre al restaurar un backup anterior a la
   destrucción, coherente con la retención máxima de datos personales en
   backups.
8. Se mide el coste del cifrado bajo el bloqueo de la cabecera (seguimiento
   operativo; no reabre el spike).

## Cierre

ADR 0019 **Aceptado**; requisitos SEG-AUD-001 y guía 23 actualizados en el
repositorio. Evidencia principal en tests de integración, no en un prototipo
desechable de esta carpeta.
