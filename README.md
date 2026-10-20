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
