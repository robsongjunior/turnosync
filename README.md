# TurnoSync — Gestor de turnos de trabajo

Aplicación para gestionar los turnos de trabajo de empleados de distintos países: alta de empleados, turnos sin
solapamientos, festivos del país de cada empleado e importación/exportación en `.xlsx`.

- **Backend:** .NET 9 (ASP.NET Core), Arquitectura Limpia en 4 proyectos (`Domain`, `Application`, `Infra`, `Api`), EF Core + SQLite.
- **Frontend:** Angular 22, una sola pantalla.
- **Documentación de la API:** Swagger en `/swagger`.

## Sobre el desarrollo

Todo el código se generó con **Claude Code**, tal y como autoriza la prueba. Sin embargo, el desarrollador **Robson Gomes**
tuvo el control total de la dirección técnica: eligió la arquitectura, la base de datos y cómo se estructura cada parte.

Por convención internacional, el **código y la base de datos están en inglés**. La interfaz y los mensajes de error, en español.

Robson es conservador en las decisiones tecnológicas del inicio de un proyecto: prefiere arquitecturas tradicionales,
simples de evolucionar y de mantener, pero siempre dejando abierta la puerta a una arquitectura más ambiciosa, como **CQRS**
o **microservicios**, si el negocio lo llega a necesitar. Por eso aquí no hay MediatR, eventos de dominio ni varios
servicios: la regla de negocio vive en el dominio, aislada, y puede moverse sin reescribirse.

## Decisiones técnicas

| Decisión | Por qué |
| --- | --- |
| **Arquitectura Limpia** en 4 proyectos (`Domain`, `Application`, `Infra`, `Api`) | Las dependencias apuntan hacia dentro: el dominio no depende de ningún paquete y la `Application` define las interfaces que la `Infra` implementa. Cambiar la base de datos o la API de festivos no toca la regla de negocio. |
| **Dominio rico** | La regla de solapamiento (`Shift.Overlaps`) y el cálculo de inicio/fin de un turno nocturno viven en la entidad, en **un único sitio**, y los usan tanto la API como la importación. Se prueban sin mocks ni base de datos. |
| **FluentValidation** en vez de DataAnnotations | Más libertad para escribir reglas (entre campos, como "fin ≠ inicio", o que dependen del reloj) y **menos impacto en el dominio**: las entidades no se llenan de atributos de validación. El **mismo validator** sirve a la API y a cada fila del Excel. |
| **`Result<T>`** para errores de negocio | Un error de negocio es un resultado esperado, no una excepción. Así cada fila de la importación tiene su propio resultado, y un `BaseController` lo traduce a 400/404/409 con `ProblemDetails`. |
| **`GlobalExceptionHandler`** | Lo inesperado devuelve un 500 sin *stack trace*: ningún detalle interno sale de la API. |
| **`TimeProvider`** inyectado | La regla "no se permiten turnos en el pasado" se prueba con un reloj fijo, sin depender de la hora real. |
| **Resiliencia en la API externa** (`Microsoft.Extensions.Http.Resilience` + caché en memoria) | Nager.Date lenta o caída no bloquea ni rompe el listado: timeout, un reintento y caché de 24 h. Las consultas de varios países van en paralelo. |
| **SQLite en archivo** con migraciones de EF Core | Base relacional real (claves foráneas, índice único parcial, transacciones) sin un contenedor extra. Migrar a PostgreSQL es cambiar el *provider* y regenerar la migración. |
| **Borrado lógico** | Conserva el historial y la integridad referencial. |
| **Calidad en el *build*** | `nullable`, advertencias como errores y un `.editorconfig` basado en las guías de Microsoft: el estilo se exige al compilar, no en la revisión. |
| **Frontend mínimo** | Angular 22 *standalone*, *signals*, sin zone.js y sin librería de UI. La API sirve el build de Angular: mismo origen, sin CORS ni nginx. |
| **Docker multi-etapa** | Un solo `docker-compose up` levanta todo, y `docker-compose run --rm tests` ejecuta las pruebas sin tener el SDK instalado. |
| **Pruebas con foco** (xUnit, NSubstitute, `WebApplicationFactory`) | Calidad antes que cantidad: todos los casos límite del solapamiento, los fallos de la API externa (red, timeout, JSON inválido) y la API completa en memoria sobre SQLite real. |

Límite conocido: dos peticiones simultáneas podrían colar un solapamiento entre la comprobación y el guardado. En producción se
resolvería con una *exclusion constraint* de PostgreSQL o una transacción serializable.

## Decisiones de negocio no definidas en el enunciado

El enunciado deja huecos. Estas son las decisiones que tomó el desarrollador:

| Tema | Decisión |
| --- | --- |
| Zona horaria | Por simplificación, **"ahora" es la hora de Madrid** (`Europe/Madrid`). No existe un mapa fiable país → zona horaria (BR o MX tienen varias). Las horas de un turno son locales del empleado y no se convierten; el horario de verano se ignora. |
| Turnos en el pasado | **No se permiten.** Crear, editar o importar un turno cuyo inicio ya pasó (según la hora de Madrid) devuelve 400. |
| Turnos nocturnos | **Se permiten.** Si la hora de fin es menor que la de inicio, el turno termina al día siguiente (22:00–06:00). Inicio = fin es inválido. |
| Solapamiento | Intervalos `[inicio, fin)`: dos turnos que solo se tocan (08–12 y 12–16) son válidos. La comprobación funciona entre días. |
| Caché de festivos | Las llamadas HTTP a Nager.Date se **cachean 24 h** por (país, año). Los fallos no se cachean. |
| Fallo de la API de festivos | Nunca rompe el listado: el turno sale con festivo **desconocido** (`isHoliday: null`) y la pantalla lo avisa. Timeout de 5 s con un reintento. |
| Qué festivos | Solo los **nacionales**, porque el empleado no tiene región. |
| Qué fecha cuenta para el festivo | La de **inicio** del turno. |
| Identificación en el Excel | Por **email**, que es único entre los empleados activos (sin distinguir mayúsculas). La importación no crea empleados. |
| País | Código ISO 3166-1 alfa-2 (`ES`, `BR`…), el que usa Nager.Date. |
| Borrado | **Lógico** (*soft delete*), para no perder historial. Borrar un empleado borra también sus turnos. |
| Importación | Informe fila a fila; una fila inválida no impide importar las demás. El archivo exportado se puede volver a importar. |
| Exportación | Filtra por la fecha de inicio del turno, con `from` y `to` obligatorios e inclusivos. No aplica el filtro de festivo, para no depender de la API externa. |
| Datos de ejemplo | Al crear la base se insertan 5 empleados (ES, BR, AR, MX, PT). **No se insertan turnos.** |

Fuera de alcance: autenticación, paginación, festivos regionales e i18n. En `samples/` hay dos Excel de ejemplo para importar
(uno válido y otro con errores). Columnas: `Email`, `Fecha`, `HoraInicio`, `HoraFin`.

## Cómo ejecutarlo

### Con Docker

```bash
docker-compose up
```

- Aplicación: <http://localhost:8080>
- Swagger: <http://localhost:8080/swagger>

La base se guarda en el volumen `turnosync-data` y se conserva entre ejecuciones. `docker-compose down -v` la borra.

### Sin Docker

Requisitos: SDK de .NET 9 y Node 22.

```bash
dotnet run --project backend/src/TurnoSync.Api
```

```bash
cd frontend
npm install
npm start
```

- API: <http://localhost:8080> (la base se crea como `turnosync.db` en el directorio desde el que se arranca).
- Frontend: <http://localhost:4200> (redirige `/api` a la API).

### Pruebas

```bash
docker-compose run --rm tests
```

O, con el SDK instalado:

```bash
dotnet test backend/TurnoSync.slnx
```

## Copiar la base de datos a tu equipo

Para analizar las tablas con un cliente SQLite (DB Browser for SQLite, DBeaver…).

> **Haz la copia después de las primeras inserciones.** La base solo trae los empleados de ejemplo: crea o importa algunos
> turnos antes de copiarla para tener datos que revisar. La copia es una foto del momento; para ver datos nuevos, repítela.

Con la aplicación en marcha (`docker-compose up`), desde la raíz del proyecto:

```bash
docker-compose cp app:/data/turnosync.db ./turnosync.db
docker-compose cp app:/data/turnosync.db-wal ./turnosync.db-wal
docker-compose cp app:/data/turnosync.db-shm ./turnosync.db-shm
```

La base usa modo WAL: los cambios más recientes pueden estar aún en `turnosync.db-wal`, así que copia los tres archivos a la
misma carpeta y abre `turnosync.db`. Si `-wal` o `-shm` no existen, basta con el `.db`.

Sin Docker no hace falta copiar nada: abre directamente `turnosync.db` en el directorio desde el que arrancaste la API.
