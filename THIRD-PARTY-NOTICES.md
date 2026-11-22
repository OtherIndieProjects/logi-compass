# Third-Party Notices / Avisos de terceros

Triple AAA Logistics Estudio is licensed under the [MIT License](LICENSE). It uses third-party components distributed under their own licenses.

> **Status: stub.** This file will be generated automatically from the dependency graph (NuGet and container images) on every release, together with the CycloneDX SBOM. Until then it lists the policy and is updated manually when a dependency is added.

## License policy / Política de licencias

- Only **permissive, OSI-approved** licenses compatible with MIT are accepted: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, MS-PL, PostgreSQL License, zlib, or equivalent.
- **Not accepted:** commercial or paid licenses, "free for non-commercial use" terms, dual licenses with commercial restrictions, source-available licenses (e.g. SSPL, BSL) and strong copyleft (GPL, AGPL) for linked components.
- Each new dependency is reviewed (license, maintenance, security) before it is added and justified in its pull request.
- CI will fail if a dependency with a non-allowed license is introduced (planned for Phase 0 CI).

Solo se aceptan licencias permisivas compatibles con MIT. Ver [`CONTRIBUTING.md`](CONTRIBUTING.md#dependencias-y-licencias).

## Components / Componentes

| Component | Version | License | Source |
|---|---|---|---|
| ClosedXML | 0.105.1 | MIT | https://github.com/ClosedXML/ClosedXML |
| EFCore.NamingConventions | 10.0.1 | Apache-2.0 | https://github.com/efcore/EFCore.NamingConventions |
| Konscious.Security.Cryptography.Argon2 | 1.3.1 | MIT | https://github.com/kmaragon/Konscious.Security.Cryptography |
| Microsoft ASP.NET Core / EF Core packages | 10.0.14 | MIT | https://github.com/dotnet/aspnetcore |
| Npgsql | 10.0.3 | PostgreSQL | https://github.com/npgsql/npgsql |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | https://github.com/npgsql/efcore.pg |

The .NET runtime and ASP.NET Core are © Microsoft Corporation and licensed under the MIT License. PostgreSQL is distributed under the PostgreSQL License and is used as an external service, not bundled.
