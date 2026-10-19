# TurnoSync — Gestor de turnos de trabajo

Aplicación para gestionar los turnos de trabajo de colaboradores de distintos países.

- **Backend:** API REST en .NET 9 (ASP.NET Core, Arquitectura Limpia, EF Core + SQLite).
- **Frontend:** una pantalla en Angular 22 (listado de turnos, formulario de alta/edición, alta de colaboradores, importar y exportar).
- **Extras del enunciado:** importación/exportación `.xlsx`, festivos del país de cada colaborador (Nager.Date), Swagger y pruebas automatizadas.

## Cómo ejecutarlo

### Con Docker (un solo comando)

```bash
docker-compose up
```

| Qué | Dónde |
| --- | --- |
| Aplicación (frontend) | <http://localhost:8080> |
| Swagger / OpenAPI | <http://localhost:8080/swagger> |

La API sirve el build de Angular desde `wwwroot`, así que frontend y API comparten origen (sin CORS y sin nginx).
Al arrancar se aplican las migraciones y se cargan 5 colaboradores de ejemplo (ES, BR, AR, MX, PT).
Los datos se guardan en el volumen `turnosync-data` y se conservan entre ejecuciones; `docker-compose down -v` los borra.

### En local (sin Docker)

Requisitos: SDK de .NET 9 y Node 22.

```bash
# API en http://localhost:8080
dotnet run --project backend/src/TurnoSync.Api

# Frontend en http://localhost:4200 (proxy de /api hacia la API)
cd frontend
npm install
npm start
```

### Ejecutar las pruebas

```bash
docker-compose run --rm tests        # sin necesidad de SDK local
dotnet test backend/TurnoSync.slnx   # exige el SDK de .NET 9
```

> Si solo tienes instalado un SDK/runtime más nuevo (por ejemplo .NET 10), el proyecto compila igualmente a `net9.0`;
> para ejecutar las pruebas fuera de Docker puedes usar `DOTNET_ROLL_FORWARD=Major dotnet test ...`.

## Requisitos del enunciado y cómo se cubren

| # | Requisito | Solución |
| --- | --- | --- |
| 1 | CRUD de colaboradores (nombre, email, país) | 5 endpoints; solo esos 3 campos. Alta también desde la interfaz |
| 2 | CRUD de turnos (colaborador, fecha, inicio, fin) | 5 endpoints; solo esos 4 campos |
| 3 | Sin solapamientos para el mismo colaborador | `Shift.Overlaps` en el dominio, aplicado en POST, PUT e importación |
| 4 | Importar `.xlsx` con informe fila a fila | Columnas `Email`, `Fecha`, `HoraInicio`, `HoraFin`; informe `imported` / `failed` con los motivos |
| 5 | Exportar `.xlsx` filtrado por rango de fechas | `GET /api/shifts/export?from=&to=`, mismas 4 columnas |
| 6 | Festivos del país del colaborador (API externa) | Nager.Date; `isHoliday` en el listado; si la API falla, `isHoliday: null` y `holidaysAvailable: false` |
| 7 | Base de datos relacional | SQLite en archivo vía EF Core (con migraciones), en un volumen de Docker |
| 8 | `docker-compose up` | Un servicio `app` (API + Angular) |
| 9 | OpenAPI/Swagger | Swashbuckle en `/swagger`, con descripciones en español tomadas de los comentarios XML |
| 10 | Pruebas automatizadas | xUnit; ver [Pruebas](#pruebas) |
| 11 | README | Este documento |

## API

| Método y ruta | Descripción | Respuestas |
| --- | --- | --- |
| `GET /api/collaborators` | Lista colaboradores | 200 |
| `GET /api/collaborators/{id}` | Detalle | 200 · 404 |
| `POST /api/collaborators` | Crea | 201 · 400 · 409 (email duplicado) |
| `PUT /api/collaborators/{id}` | Actualiza | 200 · 400 · 404 · 409 |
| `DELETE /api/collaborators/{id}` | Borrado lógico del colaborador **y de sus turnos** | 204 · 404 |
| `GET /api/shifts` | Lista todos los turnos con `isHoliday` | 200 |
| `GET /api/shifts/{id}` | Detalle | 200 · 404 |
| `POST /api/shifts` | Crea | 201 · 400 · 404 (colaborador) · 409 (solapamiento) |
| `PUT /api/shifts/{id}` | Actualiza | 200 · 400 · 404 · 409 |
| `DELETE /api/shifts/{id}` | Borrado lógico | 204 · 404 |
| `POST /api/shifts/import` | Sube un `.xlsx` (multipart, campo `file`) y devuelve el informe | 200 · 400 |
| `GET /api/shifts/export?from=&to=` | Descarga un `.xlsx` llamado `turnos_{from}_{to}.xlsx` (`from` y `to` obligatorios, `yyyy-MM-dd`) | 200 · 400 |

Los errores siguen el estándar `ProblemDetails` / `ValidationProblemDetails` de ASP.NET Core. Los mensajes están en español,
también los que genera el propio framework (cuerpo vacío, JSON mal formado, valor no válido…).

Ejemplo de turno en el listado (`GET /api/shifts`):

```json
{
  "items": [
    { "id": 1, "collaboratorId": 1, "collaboratorName": "Ana Torres", "date": "2026-10-12",
      "startTime": "22:00", "endTime": "06:00", "isHoliday": true }
  ],
  "holidaysAvailable": true
}
```

### Formato del Excel

Primera hoja, con las columnas **`Email`**, **`Fecha`**, **`HoraInicio`**, **`HoraFin`** (el encabezado se localiza por nombre,
sin distinguir mayúsculas y en cualquier orden; las columnas extra se ignoran). Fecha y hora pueden ser celdas nativas de Excel
o texto (`yyyy-MM-dd`, `HH:mm`). Las filas en blanco se ignoran.

El informe de importación indica el número de fila de la hoja y todos los motivos de cada fallo:

```json
{
  "imported": [{ "row": 2, "email": "ana@turnosync.dev" }],
  "failed":   [{ "row": 5, "email": "x@y.com", "reasons": ["Se solapa con la fila 3."] }]
}
```

Las filas válidas se guardan en una única operación; una fila inválida no impide importar las demás. El archivo exportado
usa el mismo formato (con celdas nativas de fecha y hora), así que **se puede volver a importar**.

## Decisiones de diseño

- **Arquitectura Limpia en 4 proyectos** (`Domain`, `Application`, `Infra`, `Api`). Las dependencias apuntan hacia dentro: el
  dominio no tiene ningún paquete; la Application define las interfaces (repositorios, proveedor de festivos, hoja de cálculo)
  y la Infra las implementa. La Api solo referencia la Infra en `Program.cs`, para registrar la inyección de dependencias.
  Dividí el *Application Core* de la guía de Microsoft en dos proyectos para poder probar la regla de solapamiento sin ningún
  paquete ni mock; el coste es un proyecto más.
- **Dominio rico, Application que solo orquesta.** La regla vive en las entidades: `Shift.Overlaps`, el cálculo de
  `StartsAt`/`EndsAt` y la normalización de email y país. **La regla de solapamiento existe en un único sitio** y la usan la API
  y la importación.
- **Errores de negocio como valor, no como excepción** (`Result<T>`). En la importación cada fila necesita su propio resultado.
  `BaseController` los convierte en `ProblemDetails` (400/404/409). Solo se lanzan excepciones por errores de programación
  o de infraestructura, y un `GlobalExceptionHandler` los convierte en un 500 sin *stack trace*.
- **FluentValidation** en vez de DataAnnotations: la regla "fin ≠ inicio" es entre campos y **el mismo validator** sirve a la API
  y a cada fila del Excel.
- **Regla de solapamiento.** Intervalos semiabiertos `[inicio, fin)`: dos turnos que se tocan (08–12 y 12–16) están permitidos.
  Como un turno dura menos de 24 h, solo hace falta comparar con los turnos del día anterior al siguiente. Al actualizar, el
  turno se excluye de la comparación para no chocar consigo mismo.
- **Festivos (Nager.Date).** `HttpClient` tipado con `Microsoft.Extensions.Http.Resilience` (5 s en total, 2 s por intento,
  1 reintento) y caché en memoria de 24 h por (país, año). **Un fallo nunca se cachea ni rompe el listado**: se registra un aviso y los
  turnos de ese país salen con `isHoliday: null` ("desconocido", distinto de "no es festivo"). La interfaz lo muestra con un aviso
  y un `?`. Una consulta por (país, año) presente en el listado, en paralelo.
- **Idioma.** Los identificadores del código y de la base de datos van en inglés; los comentarios, los mensajes de error (de
  la API y de la interfaz) y la propia interfaz, en español. Los validadores y los servicios ya redactan sus mensajes en español.
  Los que genera ASP.NET Core (cuerpo vacío, valores no válidos, títulos de `ProblemDetails`) y `System.Text.Json` (que además
  nombra tipos internos de .NET) los sustituye `SpanishErrorMessages`, así ninguna respuesta de error de la API sale en otro idioma.
  El idioma está fijado en el código: no hay i18n.
- **Calidad del código.** `Directory.Build.props` (`net9.0`, nullable, advertencias como errores) y un `.editorconfig` basado en las
  guías de Microsoft (Framework Design Guidelines, C# Coding Conventions, reglas de nombres e IDExxxx) que se aplica en el *build*.
- **Frontend mínimo.** Angular 22 con componentes *standalone*, *signals*, sin zone.js, Reactive Forms, sin librería de UI y sin
  router (es una sola pantalla). Llama a URLs relativas (`/api/...`): no hay CORS ni `environment.ts`.
- **Alta de colaboradores en la misma pantalla.** El enunciado pide en el frontend solo el listado, el formulario de turnos, importar
  y exportar, y la primera versión dejó los colaboradores solo en la API. Lo cambié por usabilidad: la importación **no crea**
  colaboradores (los busca por email), así que desde la interfaz solo se podía trabajar con los de ejemplo, y no se veía el flujo
  completo (dar de alta un colaborador → crear o importar sus turnos → ver los festivos de su país → exportar) sin pasar por Swagger.
  Para no añadir pantallas, el botón **"+ Nuevo colaborador"** está junto a "+ Nuevo turno" y abre, igual que este, un formulario
  debajo de la barra (nombre, email y país). La validación de verdad (país ISO existente, email repetido → 409) sigue en la API.
- **Docker.** `Dockerfile` multi-etapa: `node:22` (build de Angular) → `sdk:9.0` (publish) → `aspnet:9.0` (`final`, con Angular en
  `wwwroot`); más una etapa `test` que ejecuta `dotnet test`.

## Ambigüedades del enunciado y decisión tomada

| Ambigüedad | Decisión |
| --- | --- |
| ¿Se permiten turnos nocturnos? | **Sí.** Si la hora de fin es menor que la de inicio, el turno termina al día siguiente (22:00–06:00). Fin = inicio es inválido. La comprobación de solapamiento es correcta entre días. |
| ¿Qué pasa con los turnos al borrar un colaborador? | Se borran (lógicamente) en cascada, en una sola operación. |
| ¿Cómo se identifica al colaborador en el Excel? | Por su email, que por tanto es **único** entre los colaboradores no borrados (se compara sin distinguir mayúsculas). |
| ¿Formato del país? | Código ISO 3166-1 alfa-2 (`ES`, `BR`…), que es el que usa Nager.Date. Se normaliza a mayúsculas y se valida. |
| ¿Zonas horarias? | Las horas son locales al país del colaborador y no se convierten; el horario de verano se ignora. |
| ¿Qué fecha cuenta para el festivo? | La de inicio del turno (un turno nocturno que empieza la víspera de un festivo no se marca). |
| ¿Qué festivos? | Solo los nacionales (`global: true`), porque el colaborador no tiene región. |
| ¿Qué fecha filtra la exportación? | La fecha de inicio del turno (`date`), con `from` y `to` obligatorios e inclusivos; `from > to` devuelve 400. |
| Formato de las horas en la API | `HH:mm` (los segundos se descartan). |

## Extras más allá del enunciado (y por qué)

- **Caché de festivos:** evita una llamada externa por cada carga del listado y limita el impacto de una API lenta.
- **Servicio `tests` en `docker-compose`** (perfil `test`, no se levanta con `up`): permite ejecutar las pruebas sin tener el SDK instalado.
- **Borrado lógico (*soft delete*):** el enunciado no dice qué hacer al borrar; así no se pierde el historial y se conserva la integridad referencial.
- **Colaboradores de ejemplo (*seed*):** permiten probar los turnos, los festivos de varios países y la importación nada más arrancar.

## Persistencia

SQLite **en archivo** con migraciones de EF Core. Es una base relacional real (índice único parcial para el email, claves foráneas,
transacciones) sin necesidad de un contenedor aparte.

- **Con Docker:** `ConnectionStrings__TurnoSync=Data Source=/data/turnosync.db` en el `docker-compose.yml`, con `/data` montado en el
  volumen `turnosync-data`. La base sobrevive a reinicios, a `docker-compose down` y a nuevos builds; solo `docker-compose down -v` la borra.
  Para copiarla al equipo: `docker-compose cp app:/data/turnosync.db ./turnosync.db`.
- **En local:** `Data Source=turnosync.db` (`appsettings.json`), relativo al directorio desde el que se arranca la API (ignorado en git).
- Las migraciones se aplican al arrancar; los colaboradores de ejemplo se insertan solo al crear la base, no en cada arranque.
- Las pruebas siguen usando SQLite en memoria (una base por prueba). Con una cadena `Mode=Memory` la API mantiene abierta una conexión
  *singleton* para que la base viva mientras está arriba; con un archivo no hace falta.
- **Pasar a PostgreSQL:** sustituir `Microsoft.EntityFrameworkCore.Sqlite` por `Npgsql.EntityFrameworkCore.PostgreSQL` en `TurnoSync.Infra`, cambiar
  `UseSqlite` por `UseNpgsql`, regenerar la migración y añadir el servicio de base de datos al compose. El filtro del índice único parcial
  (`is_deleted = 0`) pasaría a `is_deleted = false`.

## Pruebas

xUnit + NSubstitute + `WebApplicationFactory`. Se prioriza la calidad sobre la cantidad: la regla de negocio y los fallos de la API externa.

- **Dominio:** `Shift` (cálculo de `StartsAt`/`EndsAt`, nocturnos, y todos los casos límite de `Overlaps`: parcial al inicio y al fin, contenido,
  que contiene, idéntico, turnos que se tocan, mismo horario con distinto colaborador, nocturno contra turno del día siguiente) y `Collaborator`.
- **Validators:** campos obligatorios, email, país ISO, inicio ≠ fin, medianoche como hora válida y el texto exacto de cada mensaje (en español).
- **Importación:** archivo mixto con planilla generada en memoria sobre SQLite real (filas válidas e inválidas, solapamiento con la base de datos y
  dentro del propio archivo, email desconocido, celdas ilegibles, varios motivos por fila, turnos borrados que no bloquean, nocturnos).
- **Excel:** ida y vuelta exacta, celdas nativas y de texto, tiempos como fracción del día (ruido de punto flotante), columnas en cualquier orden,
  archivo que no es `.xlsx`.
- **Festivos:** `HttpMessageHandler` falso (éxito, 404/204, fallo de red, JSON inválido, festivo regional ignorado, caché, el fallo no se cachea,
  cancelación) y el *pipeline* real de resiliencia (reintento, tope de reintentos, timeout cuando la API nunca responde).
- **Integración (API completa en memoria):** 409 por solapamiento en POST y PUT, borrado lógico, extremos inclusivos de la exportación, `from > to`,
  falla de festivos en un solo país, reimportación del archivo exportado, errores en español (también los del framework: cuerpo vacío,
  JSON mal formado, valor no válido, 415), Swagger en español y 500 sin filtrar detalles.

No hay pruebas automatizadas del frontend: la regla de negocio está toda en el backend y el enunciado deja la elección de las pruebas más relevantes.
El frontend se verificó a mano (crear, editar, 409, importar, exportar, fallo de Nager.Date y alta de colaboradores con email repetido y país no válido).

## Qué quedó fuera (y por qué)

- **Edición y borrado de colaboradores, y borrado de turnos, desde la interfaz:** existen en la API y en Swagger. En la interfaz solo está
  el alta de colaboradores (ver [Decisiones de diseño](#decisiones-de-diseño)), que es lo que hace falta para recorrer el flujo completo.
- **Filtros, paginación y orden elegido por el usuario:** el listado se devuelve en orden fijo (inicio del turno y luego nombre).
- **Autenticación y autorización**, i18n (la aplicación solo está en español) y pruebas del frontend.
- **Concurrencia entre dos peticiones simultáneas:** el servicio comprueba el solapamiento y luego guarda; entre ambos pasos puede colarse otra
  petición. En producción se resolvería con una *exclusion constraint* (PostgreSQL) o una transacción serializable.
- **Límite propio de tamaño de archivo o de filas en la importación:** rige el límite por defecto de ASP.NET Core.
- **MediatR, CQRS, Specification, eventos de dominio y AutoMapper:** innecesarios para este tamaño; el mapeo es manual sobre `record`s.
- **Festivos regionales** (el colaborador no tiene región) y **zonas horarias / horario de verano.**

### Limitaciones conocidas

- Mientras Nager.Date no responda, **cada listado espera hasta ~5 s**, porque los fallos no se cachean (así la siguiente carga lo reintenta).
  Una mejora sería una caché negativa corta o un *circuit breaker* con umbral más bajo.
- Los logs de las librerías (ASP.NET Core, Polly, EF Core) salen en inglés; los mensajes que escribe la propia aplicación, en español.
- La validación del país usa `RegionInfo`, que necesita datos de globalización (ICU). Las imágenes Debian de .NET lo incluyen; en un entorno con
  `InvariantGlobalization` rechazaría todos los países (las pruebas fallarían de inmediato, así que se detectaría).

## Uso de IA

- **Herramienta:** Claude Code (Anthropic), con los modelos Claude Sonnet 5.5 y Claude Opus 5.5, como asistente de desarrollo.
- **Para qué:** la planificación (alcance, decisiones, arquitectura y checklist) está escrita por el autor en un documento previo; la
  implementación (código del backend y del frontend, pruebas, `Dockerfile`/`docker-compose` y este README) se generó con la herramienta
  siguiendo ese plan. También se usó para verificar el resultado: compilación, ejecución de la suite de pruebas, prueba manual de la interfaz
  en un navegador y la ejecución real con Docker (`docker-compose up` y `docker-compose run --rm tests`).
- **Qué revisó el autor a mano:** _pendiente de completar por el autor antes de la entrega (qué partes revisó y puede explicar)._
