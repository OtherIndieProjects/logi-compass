# ADR 0006: Argon2id con compatibilidad PBKDF2

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Spike relacionado:** S2 — incorporado ([evidencia](../../tools/spikes/s2-argon2id/README.md), [estado de spikes](../es/spikes/README.md))
- **Referencias del plan:** `docs/es/PLAN.md`
- **Implementación:** `src/AAALogistics.Infrastructure/Security/Argon2PasswordHasher.cs` 

## Contexto

ASP.NET Core Identity usa PBKDF2 de forma predeterminada. El plan prefiere
Argon2id si existe una implementación mantenida, permisiva y con parámetros
operables. También necesitamos poder abrir bases creadas con el hasher estándar
sin invalidar contraseñas.

El paquete `Konscious.Security.Cryptography.Argon2` 1.3.1 declara licencia MIT
en sus metadatos NuGet y ofrece Argon2id para .NET 8, compatible con .NET 10.

## Decisión

Los hashes nuevos usan Argon2id mediante `Argon2PasswordHasher`, un
`IPasswordHasher<ApplicationUser>` registrado como singleton que delega el
cálculo en `Konscious.Security.Cryptography.Argon2` 1.3.1. El formato es
autocontenido:

```text
$argon2id$v=19$m=65536,t=3,p=2$<salt-base64>$<hash-base64>
```

Valores iniciales (`PasswordHashing` en `appsettings.json`):

- memoria: 65 536 KiB;
- iteraciones: 3;
- paralelismo: 2;
- salt aleatorio: 16 bytes;
- salida: 32 bytes;
- máximo de operaciones simultáneas por proceso: 2.

Límites:

- al arrancar, la configuración debe cumplir memoria 19–256 MiB, iteraciones
  1–10, paralelismo 1–16 y concurrencia 1–16; si no, el Host no arranca;
- al verificar, se rechazan hashes almacenados con los mismos límites de
  memoria, iteraciones y paralelismo, un salt de 16–64 bytes o un hash de
  16–64 bytes, para que un hash manipulado no pueda forzar consumos arbitrarios;
- la comparación usa tiempo constante (`CryptographicOperations.FixedTimeEquals`)
  y los bytes de la contraseña y del hash calculado se ponen a cero al terminar.

### Vía de rehash desde PBKDF2

Si un hash no tiene el prefijo `$argon2id$`, se verifica con el
`PasswordHasher<ApplicationUser>` de Identity. Cualquier verificación PBKDF2
correcta devuelve `SuccessRehashNeeded`. El login usa
`UserManager.CheckPasswordAsync`, que ante ese resultado sustituye el hash por
Argon2id. Un hash Argon2id con parámetros distintos de los configurados también
devuelve `SuccessRehashNeeded`. Por tanto no hay migración masiva ni contraseñas
en claro.

La política de contraseña exige al menos 12 caracteres y no impone reglas de
composición. El bloqueo de Identity limita intentos repetidos por cuenta
(5 intentos fallidos, 15 minutos).

### Concurrencia y denegación de servicio

Argon2id es caro en memoria a propósito, de modo que el login es un vector de
denegación de servicio. Para acotarlo, cada derivación pasa por un
`SemaphoreSlim` del singleton, que limita el número de operaciones simultáneas
por proceso. Con los valores iniciales, la memoria reservada por Argon2id
queda acotada a unos 2 × 64 MiB = 128 MiB, sea cual sea la carga.

Esto acota la memoria, pero no la latencia:

- la espera es síncrona y sin tiempo límite (`SemaphoreSlim.Wait()`): bajo una
  ráfaga, las solicitudes se encolan y retienen hilos del pool;
- el bloqueo de Identity es por cuenta y no frena intentos repartidos entre
  muchas cuentas;
- no hay limitación de frecuencia (rate limiting) en los endpoints
  de login.

Antes de exponer el servidor se debe añadir limitación de frecuencia por IP y
listener en los endpoints de login y valorar una espera asíncrona con tiempo
límite que responda 429/503 en lugar de encolar sin fin.

## Alternativas consideradas

| Alternativa | Resultado |
|---|---|
| PBKDF2 de Identity únicamente | Es una salida segura y soportada, pero no es resistente a memoria. Se conserva como compatibilidad. |
| Argon2id sin límite de concurrencia | Rechazada: varias solicitudes podrían reservar demasiada memoria. |
| Migrar todos los hashes fuera del login | Rechazada: exigiría conocer contraseñas o forzar su restablecimiento. |

## Consecuencias

- Cada verificación normal puede usar unos 64 MiB y tiene mayor coste de CPU.
- El límite de concurrencia introduce cola bajo carga. Debe medirse en el
  hardware objetivo antes de producción; no hay benchmark registrado todavía.
- La disponibilidad del login depende de la biblioteca externa y requiere
  vigilancia de vulnerabilidades y mantenimiento.
- Cambiar parámetros no invalida hashes existentes: un login correcto solicita
  rehash cuando difieren.

## Verificación

Evidencia:

- `tests/AAALogistics.Security.Tests/ScaffoldTests.cs`, clase
  `PasswordHasherTests`: hash Argon2id correcto, contraseña incorrecta
  rechazada y hash PBKDF2 de Identity que devuelve `SuccessRehashNeeded`.
- `tests/AAALogistics.Integration.Tests/ScaffoldTests.cs`, clase
  `AuthenticationEndpointTests`: crea un Admin con Argon2id sobre PostgreSQL y
  verifica el login real.
- `THIRD-PARTY-NOTICES.md` y `Directory.Packages.props` registran paquete,
  versión 1.3.1 y licencia MIT.

Pendiente de cubrir con pruebas automáticas: rechazo de hashes con parámetros
fuera de límites, comportamiento del límite de concurrencia bajo carga y rehash
PBKDF2 → Argon2id de extremo a extremo durante un login.
