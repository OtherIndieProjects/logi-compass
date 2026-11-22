# s2-argon2id

Estado: incorporado al producto.

ADR: [0006 — Argon2id con compatibilidad PBKDF2](../../../docs/adr/0006-hash-contrasenas-argon2id.md)

## Criterio

Argon2id seguro, mantenido y con licencia permisiva; si no, PBKDF2.

## Conclusión

Usar Argon2id con `Konscious.Security.Cryptography.Argon2` 1.3.1 (MIT),
parámetros acotados (por defecto m = 64 MiB, t = 3, p = 2) y máximo de dos
operaciones simultáneas por proceso. Los hashes PBKDF2 válidos de ASP.NET Core
Identity se conservan como vía de compatibilidad y se reescriben en Argon2id
tras el siguiente login correcto.

No hubo código desechable: la decisión se validó directamente con la
implementación en el producto, con las reglas del proyecto.

## Evidencia

- Implementación:
  [`src/AAALogistics.Infrastructure/Security/Argon2PasswordHasher.cs`](../../../src/AAALogistics.Infrastructure/Security/Argon2PasswordHasher.cs)
  (registro y validación de límites en
  `src/AAALogistics.Infrastructure/InfrastructureServiceCollectionExtensions.cs`).
- Pruebas automáticas:
  - `AAALogistics.Security.Tests.PasswordHasherTests`
    (`tests/AAALogistics.Security.Tests/ScaffoldTests.cs`);
  - `AAALogistics.Integration.Tests.AuthenticationEndpointTests`
    (`tests/AAALogistics.Integration.Tests/ScaffoldTests.cs`).
- Licencia y versión: `THIRD-PARTY-NOTICES.md` y `Directory.Packages.props`.

## Seguimiento

Fuera del cierre del spike, recogido en el ADR 0006:

- medir latencia y cola del login en el hardware objetivo;
- limitación de frecuencia en los endpoints de login antes de exponer el
  servidor;
- pruebas de rechazo de parámetros fuera de límites y de rehash de extremo a
  extremo.
