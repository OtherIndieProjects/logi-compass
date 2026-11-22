# Security Policy / Política de seguridad

## Reporting a vulnerability / Cómo reportar una vulnerabilidad

**Do not open public issues, discussions or pull requests for security vulnerabilities.**
**No abras issues, discusiones ni pull requests públicos para vulnerabilidades.**

Report privately through GitHub's **Private vulnerability reporting**:
repository → **Security** → **Report a vulnerability**.

Include, where possible:

- affected component (server, API, desktop client, portal, installer) and version or commit;
- description, impact and attack scenario;
- minimal steps or proof of concept to reproduce;
- whether the issue is already public or being exploited.

Do not include real personal data, production credentials or secrets in the report.

## What to expect / Qué esperar

This is a community project with best-effort support and no SLA. Our targets:

| Step | Target |
|---|---|
| Acknowledgement | within 5 business days |
| Initial assessment and severity | within 15 business days |
| Fix or mitigation for critical/high issues | as soon as practical; coordinated with the reporter |

We follow coordinated disclosure: a GitHub Security Advisory (and CVE when applicable) is published once a fix is available. Reporters are credited unless they prefer otherwise.

## Supported versions / Versiones soportadas

| Version | Supported |
|---|---|
| Latest `1.x` (p. ej. 1.2.x) | Yes — security fixes |
| Older `1.x` minors | No — please upgrade |
| Pre-1.0 tags | No |

Solo la última menor `1.x` publicada y la rama `main` reciben correcciones de seguridad.

## Scope / Alcance

In scope: code in this repository, published binaries, container images and installers.

Out of scope: third-party deployments and their configuration, findings that require physical or administrator access to the server, denial of service by volume, and social engineering. Vulnerabilities in dependencies should be reported upstream; tell us too if they affect this project.

## Safe harbour

Good-faith research that respects this policy, avoids privacy violations and service disruption, and uses only test data will not be pursued by the maintainers. Test only against your own local installation, never against third-party instances.
