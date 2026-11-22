# Contribuir

Gracias por tu interés en **AAA Logistics Studio / Logi Compass**.

Para vulnerabilidades de seguridad, no uses issues públicos: sigue [`SECURITY.md`](SECURITY.md).

## Arranque local

```bash
dotnet restore AAALogistics.slnx
dotnet build AAALogistics.slnx
dotnet test AAALogistics.slnx
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d postgres
```

- Secretos solo con `dotnet user-secrets` o variables de entorno; nunca en el repositorio.
- Usa datos de prueba, no datos reales de clientes.

## Pull requests

- Descripción clara en español de qué cambia y por qué.
- CI en verde (compilación y pruebas).
- Textos de interfaz y documentación de usuario en español; identificadores de código en inglés.
- Si tocas dependencias NuGet, actualiza [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Documentación de usuario

Guías y PDF de producto viven en [`docs/es/`](docs/es/) y [`docs/*.pdf`](docs/).  
Si cambias un flujo de oficina o API, actualiza la guía correspondiente.
