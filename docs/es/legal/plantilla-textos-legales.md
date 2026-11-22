# Plantilla de textos legales del portal de clientes

> **BORRADOR ORIENTATIVO. NO ES ASESORAMIENTO LEGAL.**
> Este documento es una plantilla para el portal web de clientes de AAA Logistics. Antes de abrir el portal a clientes reales, la empresa responsable debe completar los campos entre corchetes `[...]` y hacer que revise el texto un profesional que conozca la normativa aplicable (RGPD, LOPDGDD y LSSI-CE en España y la UE).
> El proyecto es de código abierto y no puede ofrecer un texto válido para todas las empresas. Cada empresa que lo instale es responsable de sus propios textos legales.

## Cómo usar esta plantilla

1. Sustituye todos los campos `[...]` por los datos reales de tu empresa.
2. Elimina las secciones que no apliquen (por ejemplo, transferencias internacionales si no las hay).
3. Revisa el resultado con un profesional.
4. Configura los textos finales en el portal. Cuando haya textos configurados, el registro exige marcar las dos casillas (ver el apartado 4).
5. Anota la fecha de la última actualización y conserva las versiones anteriores.

Datos que conviene tener a mano:

| Campo | Ejemplo |
|---|---|
| `[NOMBRE_EMPRESA]` | Razón social completa |
| `[NIF_CIF]` | NIF o CIF de la empresa |
| `[DIRECCION]` | Domicilio social |
| `[EMAIL_PRIVACIDAD]` | Correo para ejercer derechos de protección de datos |
| `[EMAIL_CONTACTO]` | Correo de contacto general (aviso legal, apartado 3) |
| `[TELEFONO]` | Teléfono de contacto |
| `[REGISTRO_MERCANTIL]` | Datos de inscripción registral, si procede |
| `[DOMINIO]` | Dirección web del portal |
| `[CIUDAD]` | Ciudad de los juzgados competentes (apartado 2.10) |
| `[PROVEEDOR_HOSTING]` | Empresa que aloja el servidor, si es un tercero |
| `[PROVEEDOR_EMAIL]` | Empresa que envía los correos del portal. Opcional: elimina la mención si no usáis un proveedor externo |
| `[PLAZO_CONSERVACION]` | Plazo de conservación de datos (ver el apartado 1.5) |
| `[PLAZO_BACKUPS]` | Plazo máximo de conservación de las copias de seguridad, por ejemplo 90 días (ver el apartado 1.5) |
| `[FECHA]` | Fecha de la última actualización |

---

## 1. Política de privacidad

**Última actualización:** [FECHA]

### 1.1. Quién es el responsable del tratamiento

- **Responsable:** [NOMBRE_EMPRESA]
- **NIF/CIF:** [NIF_CIF]
- **Domicilio:** [DIRECCION]
- **Correo de contacto para privacidad:** [EMAIL_PRIVACIDAD]
- **Teléfono:** [TELEFONO]

### 1.2. Qué datos tratamos

Al registrarte y usar el portal tratamos:

- **Datos de identificación y contacto:** nombre o razón social, NIF/CIF, dirección, nombre de la persona de contacto, correo electrónico y teléfono.
- **Documentación que aportes** para verificar tu alta como cliente.
- **Datos de la relación comercial:** solicitudes de pedido, pedidos, entregas y su estado.
- **Datos de acceso y seguridad:** nombre de usuario, contraseña (guardada únicamente como hash o derivación criptográfica irreversible, nunca en claro), fecha y hora de acceso, dirección IP e intentos de acceso fallidos.
- **Registro de actividad (auditoría):** acciones relevantes realizadas en el sistema, con el usuario que las hizo y cuándo.

No solicitamos datos de categorías especiales (salud, ideología, etc.). Te pedimos que no los incluyas en los campos de texto libre.

### 1.3. Para qué usamos tus datos y con qué base legal

| Finalidad | Base jurídica (RGPD) |
|---|---|
| Revisar y aprobar tu solicitud de alta como cliente | Aplicación de medidas precontractuales a petición tuya (art. 6.1.b) |
| Gestionar tus solicitudes de pedido, entregas y comunicaciones sobre ellas | Ejecución del contrato o de medidas precontractuales (art. 6.1.b) |
| Garantizar la seguridad del portal y prevenir accesos no autorizados o fraudes | Interés legítimo del responsable (art. 6.1.f) |
| Mantener un registro de actividad que permita demostrar y revisar los cambios hechos en el sistema | Interés legítimo del responsable (art. 6.1.f) |
| Cumplir obligaciones legales (fiscales, contables y otras) | Obligación legal (art. 6.1.c) |

No usamos tus datos para decisiones automatizadas ni para elaborar perfiles con efectos jurídicos sobre ti.
[Si envías comunicaciones comerciales, añade aquí esa finalidad y su base jurídica. Requiere consentimiento o una base legal específica. No la incluyas si no la usas.]

### 1.4. A quién comunicamos tus datos

- **No vendemos tus datos.**
- Pueden acceder a ellos los proveedores que nos prestan servicios necesarios para el portal, actuando como encargados del tratamiento y con contrato: [PROVEEDOR_HOSTING] (alojamiento) y [PROVEEDOR_EMAIL] (envío de correos). [Elimina la mención a `[PROVEEDOR_EMAIL]` si no usáis un proveedor externo de correo.]
- Podemos comunicar datos a las administraciones públicas y a otras entidades cuando una ley nos obligue.
- [Si los datos de pedidos se intercambian con otros sistemas de la empresa o con terceros (por ejemplo, transportistas), indícalo aquí.]

### 1.5. Cuánto tiempo conservamos tus datos

- **Solicitudes de alta rechazadas:** [PLAZO_CONSERVACION, por ejemplo 6 meses] desde su rechazo.
- **Cliente activo:** mientras mantengas la relación con nosotros.
- **Tras la baja:** durante los plazos exigidos por la ley (por ejemplo, obligaciones fiscales y contables) y, después, los suprimimos o los anonimizamos.
- **Registro de actividad (auditoría):** se conserva durante [PLAZO_CONSERVACION] por razones de seguridad y trazabilidad. Pasado ese plazo, o cuando ejerzas tu derecho de supresión y no exista obligación de conservarlo, los datos personales que contenga se eliminan o se anonimizan.

- **Copias de seguridad:** los datos personales pueden permanecer en las copias de seguridad hasta que estas expiren, como máximo [PLAZO_BACKUPS, por ejemplo 90 días]. Si ejerces tu derecho de supresión, los datos desaparecen de los sistemas en uso y de las copias cuando estas caduquen. No se restauran datos suprimidos salvo que sea imprescindible, y en ese caso se vuelven a suprimir.

### 1.6. Transferencias internacionales

[Si el servidor o los proveedores están fuera del Espacio Económico Europeo, indica aquí a qué países se transfieren los datos y bajo qué garantías (decisión de adecuación, cláusulas contractuales tipo, etc.). Si no hay transferencias, escribe: "No realizamos transferencias internacionales de datos."]

### 1.7. Tus derechos

Puedes ejercer en cualquier momento los derechos de:

- **Acceso:** saber qué datos tuyos tratamos.
- **Rectificación:** corregir datos inexactos.
- **Supresión:** pedir que eliminemos tus datos cuando proceda.
- **Oposición** al tratamiento basado en nuestro interés legítimo.
- **Limitación** del tratamiento en los casos previstos por la ley.
- **Portabilidad:** recibir tus datos en un formato estructurado.
- **Retirar tu consentimiento**, cuando el tratamiento se base en él, sin que ello afecte a lo ya realizado.

Para ejercerlos escribe a [EMAIL_PRIVACIDAD] indicando tu nombre, el derecho que ejerces y, si hace falta, una copia de un documento que acredite tu identidad. Responderemos en el plazo legal (un mes, ampliable en casos complejos).

Si consideras que tus derechos no se han atendido correctamente, puedes presentar una reclamación ante la **Agencia Española de Protección de Datos** (www.aepd.es).

### 1.8. Seguridad

Aplicamos medidas técnicas y organizativas para proteger tus datos, entre ellas: conexión cifrada (HTTPS), contraseñas almacenadas únicamente como hash o derivación criptográfica irreversible, control de acceso por roles, aislamiento de los datos de cada cliente, copias de seguridad y registro de actividad. Ningún sistema es infalible. Si se produjera una brecha de seguridad que te afectara, te lo comunicaremos en los casos y plazos que exige la ley.

### 1.9. Menores

El portal está dirigido a empresas y profesionales. No está pensado para menores de edad.

### 1.10. Cambios en esta política

Si modificamos esta política te lo indicaremos en el portal y actualizaremos la fecha. Cuando el cambio sea importante, te pediremos de nuevo que lo aceptes.

[Nota para la empresa: si mantienes esta promesa, el portal debe impedir el uso de la cuenta hasta que el cliente acepte la nueva versión. Configura la nueva versión del texto en el portal para que solicite la reaceptación.]

---

## 2. Condiciones de uso del portal

**Última actualización:** [FECHA]

### 2.1. Objeto

Estas condiciones regulan el uso del portal web de clientes de [NOMBRE_EMPRESA] (en adelante, "la Empresa"), accesible en [DOMINIO]. Su finalidad es que los clientes aprobados consulten el catálogo, soliciten productos y sigan el estado de sus pedidos.

### 2.2. Registro y aprobación

- El registro está reservado a empresas y profesionales.
- Debes aportar datos veraces, completos y actualizados. Eres responsable de la exactitud de lo que facilites.
- Tu solicitud de alta queda **pendiente de aprobación**. La Empresa puede revisar los datos y solicitar documentación adicional.
- La Empresa puede **aceptar o rechazar** la solicitud sin obligación de justificar su decisión, y puede **suspender o dar de baja** una cuenta si detecta datos falsos, un uso indebido o un incumplimiento de estas condiciones.
- Hasta que tu alta sea aprobada no podrás acceder al contenido del portal.

### 2.3. Cuenta y credenciales

- Eres responsable de custodiar tu usuario y tu contraseña y de todo lo que se haga con tu cuenta.
- No compartas tus credenciales. Si sospechas que otra persona las conoce, cámbialas y avísanos de inmediato.
- La Empresa puede bloquear una cuenta temporalmente ante intentos de acceso sospechosos.

### 2.4. Uso permitido

Te comprometes a usar el portal de forma lícita y de buena fe. Está prohibido:

- Intentar acceder a datos de otros clientes o a zonas no autorizadas.
- Alterar, sobrecargar o dañar el funcionamiento del portal o de sus sistemas.
- Usar medios automáticos para extraer información masivamente sin autorización.
- Introducir contenido ilícito, malicioso o que vulnere derechos de terceros.

### 2.5. Solicitudes de pedido

- Una **solicitud de pedido** enviada desde el portal es una petición. No supone aceptación hasta que la Empresa la **confirme**.
- La disponibilidad y los precios mostrados en el catálogo pueden cambiar hasta la confirmación. La Empresa procurará mantenerlos actualizados, pero no garantiza que estén libres de errores.
- Los plazos de entrega, las condiciones de pago y cualquier otra condición comercial se acuerdan directamente con la Empresa, fuera del portal. El portal no registra esa información.
- [Adapta este apartado a vuestras condiciones comerciales reales. Si en el futuro el portal registra plazos o condiciones de pago, actualiza este texto.]

### 2.6. Disponibilidad del servicio

La Empresa procura que el portal esté disponible, pero no garantiza un funcionamiento ininterrumpido. Puede haber paradas por mantenimiento, actualizaciones o causas ajenas a su control.

### 2.7. Propiedad intelectual

- Los contenidos del portal (catálogo, textos, imágenes, marcas y logotipos de la Empresa) pertenecen a la Empresa o a sus licenciantes, y no puedes reproducirlos ni explotarlos sin autorización.
- El software AAA Logistics es de código abierto y se distribuye bajo licencia MIT. Esa licencia se aplica al software, no a los contenidos ni a los datos de la Empresa.

### 2.8. Responsabilidad

La Empresa no responde de los daños derivados de un uso indebido del portal, de la pérdida de tus credenciales por causas imputables a ti, ni de interrupciones del servicio ajenas a su control. Esto no limita la responsabilidad que la ley no permita excluir.

### 2.9. Modificaciones

La Empresa puede modificar estas condiciones. Los cambios se publicarán en el portal con la nueva fecha. Si el cambio es relevante, se te pedirá aceptarlo de nuevo para seguir usando el portal.

[Nota para la empresa: el portal debe bloquear el uso de la cuenta hasta que el cliente acepte la nueva versión de las condiciones.]

### 2.10. Legislación y jurisdicción

Estas condiciones se rigen por la legislación [española]. Para cualquier controversia, las partes se someten a los juzgados y tribunales de [CIUDAD], sin perjuicio de los derechos que la ley reconozca al consumidor, si procede.

---

## 3. Aviso legal e información general

Información que la normativa de servicios de la sociedad de la información (LSSI-CE) exige mostrar a quien ofrece servicios por internet:

- **Titular del portal:** [NOMBRE_EMPRESA]
- **NIF/CIF:** [NIF_CIF]
- **Domicilio:** [DIRECCION]
- **Correo electrónico:** [EMAIL_CONTACTO]
- **Teléfono:** [TELEFONO]
- **Datos registrales:** [REGISTRO_MERCANTIL]

### Uso de cookies y tecnologías similares

El portal utiliza únicamente **cookies técnicas imprescindibles**: mantener la sesión iniciada y proteger los formularios frente a ataques. No usa cookies de analítica, publicidad ni seguimiento. Al ser cookies estrictamente necesarias para prestar el servicio solicitado, no requieren consentimiento, pero te informamos de su uso.

[Si en el futuro se añade analítica, publicidad o cualquier cookie no técnica, hay que actualizar este apartado y mostrar un aviso con opción de aceptar o rechazar antes de instalarlas.]

---

## 4. Texto de aceptación en el formulario de registro

Textos que muestra el portal junto a las dos casillas obligatorias del registro. Son casillas separadas y ninguna puede venir marcada de antemano:

> ☐ He leído y acepto las [Condiciones de uso](#condiciones-de-uso).
>
> ☐ He leído y comprendo la [Política de privacidad](#politica-de-privacidad).

Estas casillas sirven para dejar constancia de que el cliente acepta las condiciones y ha recibido la información sobre privacidad. **No son un consentimiento para tratar sus datos.** El tratamiento necesario para el alta y la relación comercial se basa en las medidas precontractuales y en la ejecución del contrato (art. 6.1.b RGPD), no en el consentimiento. Si alguna vez necesitas un consentimiento (por ejemplo, para comunicaciones comerciales), pídelo con una casilla aparte, opcional y no premarcada.

Información básica en capas, para mostrar debajo del formulario (recomendada por la AEPD):

| Apartado | Texto |
|---|---|
| **Responsable** | [NOMBRE_EMPRESA] |
| **Finalidad** | Gestionar tu solicitud de alta como cliente y, una vez aprobada, tus pedidos y la relación comercial |
| **Legitimación** | Aplicación de medidas precontractuales y ejecución del contrato |
| **Destinatarios** | No se ceden datos a terceros salvo obligación legal. Los proveedores de servicios acceden como encargados del tratamiento |
| **Derechos** | Acceso, rectificación, supresión, oposición, limitación y portabilidad, escribiendo a [EMAIL_PRIVACIDAD] |
| **Más información** | Política de privacidad completa en [DOMINIO]/privacidad |

---

## 5. Lista de comprobación antes de abrir el portal

- [ ] Todos los campos `[...]` están completados.
- [ ] Un profesional ha revisado los textos.
- [ ] Los textos están configurados en el portal y el registro exige aceptarlos.
- [ ] El portal funciona con HTTPS válido.
- [ ] Se ha definido y aplicado el plazo de conservación, incluido el del registro de auditoría y el de las copias de seguridad (`[PLAZO_BACKUPS]`).
- [ ] Existe un procedimiento para atender solicitudes de derechos (acceso, supresión, etc.) en el plazo legal.
- [ ] Hay un contrato de encargado del tratamiento con cada proveedor que accede a datos personales.
- [ ] La empresa ha valorado si debe llevar un registro de actividades de tratamiento y si necesita un delegado de protección de datos.
- [ ] Se guardan las versiones anteriores de los textos y la fecha de aceptación de cada cliente.
