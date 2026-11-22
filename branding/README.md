# Marca

Producto por defecto: **AAA Logistics Studio** (`applicationName` / `shortName`). **Logi Compass** es la brújula logística dentro del Studio (`tagline`).

La marca (nombre, empresa, logo y colores) es configurable por instalación. Esta carpeta contiene los valores por defecto del proyecto. La guía 11 (Personalización de marca) describe el procedimiento.

## Ficheros

| Fichero | Contenido |
|---|---|
| `brand.json` | Nombre de la aplicación, empresa, rutas de logo y paleta de colores |
| `logo.svg`, `logo-dark.svg`, `icon.png` | Logos para modo claro, oscuro e icono (pendientes; aún no incluidos) |

## Personalizar

1. Copia `brand.json` y edita sus valores. No incluyas secretos ni datos personales.
2. Sustituye los logos por los tuyos manteniendo los nombres o actualiza las rutas en `logo`. Recomendado: SVG para logos y PNG cuadrado de 512×512 para el icono.
3. Ajusta `colors` con valores hexadecimales `#RRGGBB`.
4. Comprueba el **contraste WCAG 2.2 AA**: al menos 4,5:1 para texto normal y 3:1 para texto grande y elementos de interfaz, en modo claro y oscuro.

## Paleta por defecto

| Clave | Color | Uso |
|---|---|---|
| `primary` | `#1E5AA8` | Acciones principales, enlaces, selección |
| `primaryDark` | `#12315A` | Barra lateral, cabeceras |
| `primaryLight` | `#E8F0FB` | Fondos destacados |
| `neutral.background` | `#F4F6F8` | Fondo de la aplicación |
| `neutral.border` | `#D5DBE3` | Bordes y separadores |
| `neutral.textSecondary` | `#5B6573` | Texto secundario |
| `neutral.text` | `#1F2630` | Texto principal |
| `status.success` | `#2E9E6B` | Éxito, contabilizado |
| `status.warning` | `#E0A100` | Advertencias (p. ej., falta de stock al confirmar) |
| `status.error` | `#D64545` | Errores y acciones destructivas |

> El estado no debe comunicarse solo con color: acompáñalo de icono o texto.

## Marcas registradas

La licencia MIT cubre el código, no concede derechos sobre nombres o logos de terceros. Si distribuyes una versión modificada, usa tu propia marca.
