CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE TABLE "ClientesProyeccion" (
        "Id" uuid NOT NULL,
        "Nombre" character varying(150) NOT NULL,
        "Estado" boolean NOT NULL,
        "Eliminado" boolean NOT NULL,
        "Version" bigint NOT NULL,
        CONSTRAINT "PK_ClientesProyeccion" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE TABLE "Cuentas" (
        "Id" uuid NOT NULL,
        "ClienteId" uuid NOT NULL,
        "NumeroCuenta" character varying(30) NOT NULL,
        "TipoCuenta" character varying(20) NOT NULL,
        "SaldoInicial" numeric(18,2) NOT NULL,
        "SaldoDisponible" numeric(18,2) NOT NULL,
        "Estado" boolean NOT NULL,
        "Version" bigint NOT NULL,
        "UltimaSecuencia" bigint NOT NULL,
        CONSTRAINT "PK_Cuentas" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Cuentas_Saldo" CHECK ("SaldoDisponible" >= 0 AND "SaldoInicial" >= 0),
        CONSTRAINT "FK_Cuentas_ClientesProyeccion_ClienteId" FOREIGN KEY ("ClienteId") REFERENCES "ClientesProyeccion" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE TABLE "Movimientos" (
        "Id" uuid NOT NULL,
        "CuentaId" uuid NOT NULL,
        "Fecha" timestamp with time zone NOT NULL,
        "TipoMovimiento" character varying(20) NOT NULL,
        "Valor" numeric(18,2) NOT NULL,
        "Saldo" numeric(18,2) NOT NULL,
        "Secuencia" bigint NOT NULL,
        CONSTRAINT "PK_Movimientos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Movimientos_Importe" CHECK ("Valor" <> 0 AND "Saldo" >= 0),
        CONSTRAINT "FK_Movimientos_Cuentas_CuentaId" FOREIGN KEY ("CuentaId") REFERENCES "Cuentas" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE TABLE "AuditoriaMovimientos" (
        "Id" uuid NOT NULL,
        "MovimientoId" uuid NOT NULL,
        "ValorAnterior" numeric(18,2) NOT NULL,
        "ValorNuevo" numeric(18,2) NOT NULL,
        "Fecha" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AuditoriaMovimientos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AuditoriaMovimientos_Movimientos_MovimientoId" FOREIGN KEY ("MovimientoId") REFERENCES "Movimientos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE INDEX "IX_AuditoriaMovimientos_MovimientoId" ON "AuditoriaMovimientos" ("MovimientoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE INDEX "IX_Cuentas_ClienteId" ON "Cuentas" ("ClienteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE UNIQUE INDEX "IX_Cuentas_NumeroCuenta" ON "Cuentas" ("NumeroCuenta");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE INDEX "IX_Movimientos_CuentaId_Fecha" ON "Movimientos" ("CuentaId", "Fecha");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    CREATE UNIQUE INDEX "IX_Movimientos_CuentaId_Secuencia" ON "Movimientos" ("CuentaId", "Secuencia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912012000_InitialAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260912012000_InitialAccounts', '8.0.11');
    END IF;
END $EF$;
COMMIT;

