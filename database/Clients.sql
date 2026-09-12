CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912011500_InitialClients') THEN
    CREATE TABLE "Clientes" (
        "Id" uuid NOT NULL,
        "PasswordHash" character varying(500) NOT NULL,
        "Estado" boolean NOT NULL,
        "Eliminado" boolean NOT NULL,
        "Version" bigint NOT NULL,
        "Nombre" character varying(150) NOT NULL,
        "Genero" character varying(30) NOT NULL,
        "Edad" integer NOT NULL,
        "Identificacion" character varying(30) NOT NULL,
        "Direccion" character varying(250) NOT NULL,
        "Telefono" character varying(30) NOT NULL,
        CONSTRAINT "PK_Clientes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912011500_InitialClients') THEN
    CREATE TABLE "Outbox" (
        "Id" uuid NOT NULL,
        "Payload" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        CONSTRAINT "PK_Outbox" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912011500_InitialClients') THEN
    CREATE UNIQUE INDEX "IX_Clientes_Identificacion" ON "Clientes" ("Identificacion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912011500_InitialClients') THEN
    CREATE INDEX "IX_Outbox_PublishedAt_CreatedAt" ON "Outbox" ("PublishedAt", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260912011500_InitialClients') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260912011500_InitialClients', '8.0.11');
    END IF;
END $EF$;
COMMIT;

