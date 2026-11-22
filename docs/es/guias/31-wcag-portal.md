# Guía 31: Accesibilidad WCAG 2.2 AA (portal)

| | |
|---|---|
| **Audiencia** | UX / QA |
| **Versión** | diseño del producto
| **Última revisión** | 2026-11-01 |

## Objetivo

Medir y corregir el Portal Blazor hasta WCAG 2.2 AA (RNF-ACC-001).

## Checklist medible

1. Contraste texto/fondo ≥ 4.5:1 (axe / Accessibility Insights).
2. Todos los controles con nombre accesible (`aria-label` / `AutomationProperties` en Avalonia).
3. Navegación por teclado sin trampas; foco visible.
4. Errores de formulario asociados al campo.
5. Sin captcha visual sin alternativa PoW/honeypot.

## Evidencia

Adjunte informe axe en CI o carpeta `docs/es/evidencias/` por release industrial.

## Véase también

- [06 Cliente externo](06-cliente-externo.md)
