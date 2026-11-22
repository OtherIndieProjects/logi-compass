# ADR 0011: Licencia MIT

- **Estado:** Aceptado
- **Fecha:** 2026-10-28
- **Referencias del plan:** `docs/es/PLAN.md`

## Contexto

El proyecto se publica en GitHub como software libre de uso libre, incluido el uso comercial por empresas que lo instalen y adapten. Debe ser sencillo de entender, compatible con el ecosistema .NET y no imponer obligaciones que desanimen su adopción.

## Decisión

El código, la documentación y los recursos del repositorio se publican bajo la **licencia MIT** (`LICENSE`), con el titular «Triple AAA Logistics Estudio contributors».

Consecuencia directa: solo se aceptan dependencias con licencias permisivas compatibles con MIT. Se rechazan dependencias comerciales, de uso no comercial, *source-available* o con copyleft fuerte en componentes enlazados. Los avisos se mantienen en `THIRD-PARTY-NOTICES.md`.

## Alternativas consideradas

| Alternativa | Motivo del descarte |
|---|---|
| Apache-2.0 | Válida y con concesión de patentes, pero más extensa; MIT es más simple y común en .NET. Se acepta en dependencias. |
| GPL-3.0 / AGPL-3.0 | Obligaciones de redistribución que limitan la adopción y la integración empresarial. |
| Licencia propia o *source-available* | No es open source según OSI. |

## Consecuencias

- **Positivas:** máxima libertad de uso, adopción y contribución; compatibilidad con la mayoría de dependencias .NET.
- **Negativas:** terceros pueden crear derivados cerrados; no hay concesión explícita de patentes.
- **Mitigaciones:** revisión de licencias de dependencias en cada PR y escaneo automático en CI.

## Verificación

- Escaneo de licencias de dependencias en CI.
- SBOM CycloneDX y `THIRD-PARTY-NOTICES.md` en cada publicación.
