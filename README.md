# Prueba técnica Devsu

El proyecto está hecho en .NET 8 con Entity Framework Core, PostgreSQL y RabbitMQ. Está separado en dos servicios: Clients para persona y cliente, y Accounts para cuentas, movimientos y reportes. Cada uno tiene su propia base de datos.

Para ejecutarlo se necesita Docker con contenedores Linux. Abrir una terminal dentro de la carpeta devsu-bank y ejecutar:

```powershell
docker compose up --build -d
docker compose ps
```

La primera vez puede tardar por la descarga de las imágenes. Las tablas se crean con las migraciones al iniciar las APIs. No hace falta instalar .NET si se ejecuta todo con Docker.

Las direcciones para probar son:

- Clientes: http://localhost:5001/swagger
- Cuentas y movimientos: http://localhost:5002/swagger
- RabbitMQ: http://localhost:15672

El usuario y contraseña locales de RabbitMQ y PostgreSQL son `bank` y `bank-local-only`. Si se necesitan cambiar, copiar .env.example como .env y editarlo antes del primer arranque. PostgreSQL de clientes usa el puerto 5433 y la base clients; el de cuentas usa 5434 y la base accounts.

Para revisar los logs o detener los servicios:

```powershell
docker compose logs --tail=100 clients-api accounts-api
docker compose down
```

Los datos quedan guardados en los volúmenes. No agregar -v al comando de apagado si se quieren conservar.

Para probar con Postman, importar estos dos archivos y seleccionar el entorno Local:

- postman/Bank.postman_collection.json
- postman/Local.postman_environment.json

Ejecutar la colección en orden desde el Runner, con un delay de 1000 ms. Los ejemplos de las solicitudes y sus validaciones ya están incluidos. Primero se crea el cliente, después su cuenta y luego los movimientos. El cambio de cliente llega a Accounts por RabbitMQ; si la cuenta devuelve 409 porque todavía no se sincronizó el cliente, esperar un momento y repetir.

Los endpoints son /clientes, /cuentas, /movimientos y /reportes. También aceptan el prefijo /api. Clientes tiene CRUD; cuentas y movimientos permiten crear, consultar y actualizar. Los identificadores son UUID. La eliminación del cliente es una baja lógica para conservar el historial.

Para un depósito se envía un valor positivo y para un retiro uno negativo. Se actualiza el saldo al guardar el movimiento. Si no alcanza el saldo, responde 422 con el mensaje "Saldo no disponible". No se permiten movimientos de valor cero ni importes con más de dos decimales.

Para consultar el estado de cuenta, reemplazar UUID por el identificador del cliente:

```text
http://localhost:5002/reportes?cliente=UUID&fecha=2022-02-01,2022-02-28
```

El reporte devuelve JSON con las cuentas, saldos y movimientos. El rango incluye ambos días y las fechas se manejan en UTC. El saldo de cierre corresponde al final del periodo consultado.

Las pruebas están en tests/Bank.UnitTests y tests/Bank.IntegrationTests. Para ejecutarlas localmente se necesita un SDK compatible con .NET 8 y Docker activo, porque las de integración levantan PostgreSQL con Testcontainers:

```powershell
dotnet restore Bank.sln
dotnet build Bank.sln -c Release
dotnet test Bank.sln -c Release --no-build
```

Se incluyen pruebas de la entidad Cliente y de operaciones de la API, saldos, reportes y concurrencia.

El archivo BaseDatos.sql contiene el esquema de las bases. El arranque con Docker ya aplica las migraciones, pero también se puede ejecutar el script manualmente desde PowerShell con los contenedores levantados:

```powershell
Get-Content -Raw BaseDatos.sql | docker compose exec -T clients-db psql -U bank -d postgres -v ON_ERROR_STOP=1 -v clients=1
Get-Content -Raw BaseDatos.sql | docker compose exec -T accounts-db psql -U bank -d postgres -v ON_ERROR_STOP=1 -v accounts=1
```

El código se organiza en Domain, Application, Infrastructure y Api. El outbox guarda los cambios de cliente pendientes de publicar si RabbitMQ no está disponible. Las versiones permiten ignorar eventos repetidos y detectar conflictos al actualizar saldos. Para reintentar un movimiento sin duplicarlo se puede enviar Idempotency-Key con el mismo UUID.

La actualización de movimientos permite corregir solo el último y guarda auditoría; para uno anterior se registra un movimiento compensatorio. Para mayor volumen habría que limitar los reportes y coordinar varias instancias del publicador. El despliegue incluido usa una instancia por componente.
