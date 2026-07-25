USE [TecAjalpanHorarios];
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'[Recursos].[DocentesCarreras]', N'U') IS NULL
BEGIN
    CREATE TABLE [Recursos].[DocentesCarreras]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [DocenteId] UNIQUEIDENTIFIER NOT NULL,
        [CarreraId] UNIQUEIDENTIFIER NOT NULL,
        [EsPrincipal] BIT NOT NULL CONSTRAINT [DF_DocentesCarreras_EsPrincipal] DEFAULT (0),
        [UsuarioCrea] NVARCHAR(MAX) NOT NULL,
        [FechaCrea] DATETIME2 NOT NULL,
        [UsuarioModifica] NVARCHAR(MAX) NULL,
        [FechaModifica] DATETIME2 NULL,
        [Eliminado] BIT NOT NULL,
        [UsuarioElimina] NVARCHAR(MAX) NULL,
        [FechaElimina] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_DocentesCarreras] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocentesCarreras_Docentes_DocenteId]
            FOREIGN KEY ([DocenteId]) REFERENCES [Recursos].[Docentes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DocentesCarreras_Carreras_CarreraId]
            FOREIGN KEY ([CarreraId]) REFERENCES [Catalogos].[Carreras] ([Id])
    );
END;

IF COL_LENGTH(N'Recursos.DocentesCarreras', N'EsPrincipal') IS NULL
BEGIN
    ALTER TABLE [Recursos].[DocentesCarreras]
        ADD [EsPrincipal] BIT NOT NULL
            CONSTRAINT [DF_DocentesCarreras_EsPrincipal] DEFAULT (0);
END;

;WITH PrimeraCarrera AS
(
    SELECT [Id],
           ROW_NUMBER() OVER (PARTITION BY [DocenteId] ORDER BY [FechaCrea], [Id]) AS Numero
    FROM [Recursos].[DocentesCarreras]
    WHERE [Eliminado] = 0
)
UPDATE dc
SET [EsPrincipal] = 1
FROM [Recursos].[DocentesCarreras] dc
INNER JOIN PrimeraCarrera pc ON pc.[Id] = dc.[Id] AND pc.Numero = 1
WHERE NOT EXISTS
(
    SELECT 1
    FROM [Recursos].[DocentesCarreras] principal
    WHERE principal.[DocenteId] = dc.[DocenteId]
      AND principal.[EsPrincipal] = 1
      AND principal.[Eliminado] = 0
);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_DocentesCarreras_CarreraId'
      AND [object_id] = OBJECT_ID(N'[Recursos].[DocentesCarreras]')
)
    CREATE INDEX [IX_DocentesCarreras_CarreraId]
        ON [Recursos].[DocentesCarreras] ([CarreraId]);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_DocentesCarreras_DocenteId_CarreraId'
      AND [object_id] = OBJECT_ID(N'[Recursos].[DocentesCarreras]')
)
    CREATE UNIQUE INDEX [IX_DocentesCarreras_DocenteId_CarreraId]
        ON [Recursos].[DocentesCarreras] ([DocenteId], [CarreraId])
        WHERE [Eliminado] = 0;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_DocentesCarreras_DocenteId'
      AND [object_id] = OBJECT_ID(N'[Recursos].[DocentesCarreras]')
)
    CREATE UNIQUE INDEX [IX_DocentesCarreras_DocenteId]
        ON [Recursos].[DocentesCarreras] ([DocenteId])
        WHERE [EsPrincipal] = 1 AND [Eliminado] = 0;

IF OBJECT_ID(N'[Recursos].[JornadasDocentes]', N'U') IS NULL
BEGIN
    CREATE TABLE [Recursos].[JornadasDocentes]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [DisponibilidadDocenteId] UNIQUEIDENTIFIER NOT NULL,
        [Dia] TINYINT NOT NULL,
        [HoraInicio] TIME NOT NULL,
        [HoraFin] TIME NOT NULL,
        [UsuarioCrea] NVARCHAR(MAX) NOT NULL,
        [FechaCrea] DATETIME2 NOT NULL,
        [UsuarioModifica] NVARCHAR(MAX) NULL,
        [FechaModifica] DATETIME2 NULL,
        [Eliminado] BIT NOT NULL,
        [UsuarioElimina] NVARCHAR(MAX) NULL,
        [FechaElimina] DATETIME2 NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_JornadasDocentes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JornadasDocentes_DisponibilidadesDocentes_DisponibilidadDocenteId]
            FOREIGN KEY ([DisponibilidadDocenteId])
            REFERENCES [Recursos].[DisponibilidadesDocentes] ([Id])
            ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_JornadasDocentes_DisponibilidadDocenteId_Dia]
        ON [Recursos].[JornadasDocentes] ([DisponibilidadDocenteId], [Dia])
        WHERE [Eliminado] = 0;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724231000_AgregarDocentesCarreras'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724231000_AgregarDocentesCarreras', N'10.0.10');
END;

COMMIT TRANSACTION;
GO

SELECT
    OBJECT_ID(N'[Recursos].[DocentesCarreras]', N'U') AS DocentesCarreras,
    OBJECT_ID(N'[Recursos].[JornadasDocentes]', N'U') AS JornadasDocentes;
GO
