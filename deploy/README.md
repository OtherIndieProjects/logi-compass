# Despliegue

Scripts y configuración para PostgreSQL local y, opcionalmente, el Host en Linux como servicio systemd.

## PostgreSQL con Docker (desarrollo / laboratorio)

1. Copie `.env.example` a `.env` y ponga contraseñas largas propias.
2. Arranque:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d postgres
```

3. El script `init-db.sh` crea el rol de aplicación `aaa_app` (sin superusuario ni `BYPASSRLS`) la primera vez que se crea el volumen.
4. En el Host use una cadena de conexión con **`aaa_app`**, nunca con el usuario bootstrap.

Puerto publicado solo en loopback: `127.0.0.1:5432`.

## Restaurar un dump

Los backups de producción del Host suelen ir cifrados (`.enc`). Prefiera el comando del propio Host:

```bash
dotnet run --project src/AAALogistics.Host -- restore --file archivo.dump.enc --confirm YES
```

`restore-postgres.sh` solo acepta un dump ya descifrado y exige la palabra exacta `YES` como primer argumento.

## Host como servicio (Linux)

1. Publique el Host (`dotnet publish` / paquete self-contained).
2. Como root:

```bash
sudo ./deploy/install-linux.sh /ruta/al/publish
```

Instala bajo `/opt/aaalogistics`, crea el usuario de sistema `aaalogistics` y habilita la unidad `aaalogistics.service` (API en `127.0.0.1:5088` por defecto).

Ajuste secretos y cadena de BD con variables de entorno o ficheros fuera del repositorio.

## Relacionado

- Guía de instalación del servidor: [`docs/es/guias/01-instalacion-servidor.md`](../docs/es/guias/01-instalacion-servidor.md)
- Despliegue y migración: [`docs/es/guias/10-despliegue-migracion.md`](../docs/es/guias/10-despliegue-migracion.md)
- Arquitectura: [`docs/es/arquitectura.md`](../docs/es/arquitectura.md)
