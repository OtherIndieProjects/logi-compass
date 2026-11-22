# Guía 16: Activar SMTP

| | |
|---|---|
| **Audiencia** | Administrador |
| **Versión del producto** | 1.2 |
| **Última revisión** | 2026-10-31 |
| **Requisitos cubiertos** | —  |
| **Permisos necesarios** | Según rol (ver [guía 14](14-roles-permisos.md)) |
| **Tiempo estimado** | 30 min |

## Para qué sirve

Correo transaccional.

## Antes de empezar

Requisitos previos:

- [ ] Servidor SMTP

> **Importante:** las contabilizaciones no se editan; revierta con el comando adecuado.

## Pasos

1. Lab: `Email:Mode=File` (escribe JSON/eml bajo `Email:FileDirectory`) o `Disabled`.

2. Industrial (diseño del producto.

3. Pruebe un flujo que genere correo (registro) y verifique fichero o bandeja.

## Resultado esperado

`File`: fichero en disco. `Smtp`: mensaje entregado al servidor configurado (staging).

## Solución de problemas

| Síntoma o código | Causa probable | Qué hacer |
|---|---|---|
| Host no arranca con Smtp | Faltan `Email:SmtpHost` / `Email:SmtpFrom` | Complete ambos; o use `File` / `Disabled` |
| Sin fichero en disco | Modo no es `File` o carpeta incorrecta | Revise `Email:Mode=File` y `Email:FileDirectory` |
| Auth SMTP rechazada | Usuario/clave o STARTTLS | Revise `SmtpUserName`/`SmtpPassword`/`SmtpUseStartTls`/`SmtpPort` |

Si el problema persiste, consulta la [guía 13 (FAQ y errores)](13-faq-errores.md) o contacta con la administración del sistema.

## Limitaciones

`Smtp` es bridge lab/staging operable. Certificados mTLS y plantillas productivas siguen fuera (diseño del producto. Registro manual posible sin correo.

## Véase también

- [03 Admin sistema](03-administrador-sistema.md)
