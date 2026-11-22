# s5-singlefile-service

Prueba de publicación autocontenida (`linux-x64` / `win-x64`).

El Host usa **`SingleInstanceLock`** (`Data/aaa-host.lock`): una segunda
instancia se rechaza con mensaje claro (`SingleInstanceLockTests`).

## Resultado

| Criterio | Linux x64 | Windows x64 |
|---|---|---|
| Publicación single-file | OK | OK |
| Arranque + health | OK | OK |
| Bloqueo de segunda instancia | OK | OK |

## Uso en producto

El empaquetado Windows de LogisticSolution y el Host publicado siguen este
camino. Detalle de arquitectura: `docs/es/arquitectura.md`.
