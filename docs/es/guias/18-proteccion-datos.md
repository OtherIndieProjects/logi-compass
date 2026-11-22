# Guía 18: Protección de datos y brechas

| | |
|---|---|
| **Audiencia** | Administrador / DPO |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | SEG-AUD-001
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 45 min |

## Para qué sirve

PII, crypto-shred y retención.

## Antes de empezar

Requisitos previos:

- [ ] Acceso acorde al rol descrito.

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Identifique PII cifrada en auditoría.

2. Procedimiento de supresión por persona.

3. Política de backups post-shred.

## Drill de aula (crypto-shred)

1. Localizar un `personId` (cliente) con entradas cifradas en auditoría.
2. Ejecutar el shred vía Host / servicio de auditoría (`CryptoShredPerson`).
3. Comprobar que el payload ya no se descifra y que `verify-audit` sigue OK
   (la cadena no se reescribe: solo se destruye la clave).
4. Documentar que un restore de backup **anterior** al shred puede reintroducir
   la clave: aplicar retención y procedimiento post-restore.

## Resultado esperado

`verify-audit` OK tras shred.

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| `Error inesperado` | Configuración o red | Revise logs del Host y guía 13 |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

Restore antiguo puede revertir shred.

## Véase también

- [23 Auditoría](23-auditoria.md)
