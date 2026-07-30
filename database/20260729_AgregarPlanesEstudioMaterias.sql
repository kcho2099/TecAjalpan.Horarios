USE [TecAjalpanHorarios];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'Catalogos.Materias', N'HorasTeoricas') IS NULL
    ALTER TABLE [Catalogos].[Materias] ADD [HorasTeoricas] tinyint NOT NULL
        CONSTRAINT [DF_Materias_HorasTeoricas] DEFAULT (0);
IF COL_LENGTH(N'Catalogos.Materias', N'HorasPracticas') IS NULL
    ALTER TABLE [Catalogos].[Materias] ADD [HorasPracticas] tinyint NOT NULL
        CONSTRAINT [DF_Materias_HorasPracticas] DEFAULT (0);

UPDATE [Catalogos].[Materias]
SET [HorasTeoricas] = [HorasSemanales]
WHERE [HorasTeoricas] = 0 AND [HorasPracticas] = 0;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reticulas_Clave'
    AND object_id=OBJECT_ID(N'[Catalogos].[Reticulas]'))
    DROP INDEX [IX_Reticulas_Clave] ON [Catalogos].[Reticulas];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Materias_Clave'
    AND object_id=OBJECT_ID(N'[Catalogos].[Materias]'))
    DROP INDEX [IX_Materias_Clave] ON [Catalogos].[Materias];

IF OBJECT_ID(N'[Catalogos].[MateriasModalidades]', N'U') IS NULL
BEGIN
    CREATE TABLE [Catalogos].[MateriasModalidades](
        [Id] uniqueidentifier NOT NULL,
        [MateriaId] uniqueidentifier NOT NULL,
        [ModalidadId] uniqueidentifier NOT NULL,
        [UsuarioCrea] nvarchar(max) NOT NULL,
        [FechaCrea] datetime2 NOT NULL,
        [UsuarioModifica] nvarchar(max) NULL,
        [FechaModifica] datetime2 NULL,
        [Eliminado] bit NOT NULL,
        [UsuarioElimina] nvarchar(max) NULL,
        [FechaElimina] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MateriasModalidades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MateriasModalidades_Materias] FOREIGN KEY ([MateriaId])
            REFERENCES [Catalogos].[Materias]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MateriasModalidades_Modalidades] FOREIGN KEY ([ModalidadId])
            REFERENCES [Catalogos].[Modalidades]([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reticulas_CarreraId_Clave'
    AND object_id=OBJECT_ID(N'[Catalogos].[Reticulas]'))
    CREATE UNIQUE INDEX [IX_Reticulas_CarreraId_Clave]
        ON [Catalogos].[Reticulas]([CarreraId],[Clave]) WHERE [Eliminado]=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Materias_ReticulaId_Clave'
    AND object_id=OBJECT_ID(N'[Catalogos].[Materias]'))
    CREATE UNIQUE INDEX [IX_Materias_ReticulaId_Clave]
        ON [Catalogos].[Materias]([ReticulaId],[Clave]) WHERE [Eliminado]=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_MateriasModalidades_MateriaId_ModalidadId'
    AND object_id=OBJECT_ID(N'[Catalogos].[MateriasModalidades]'))
    CREATE UNIQUE INDEX [IX_MateriasModalidades_MateriaId_ModalidadId]
        ON [Catalogos].[MateriasModalidades]([MateriaId],[ModalidadId]) WHERE [Eliminado]=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_MateriasModalidades_ModalidadId'
    AND object_id=OBJECT_ID(N'[Catalogos].[MateriasModalidades]'))
    CREATE INDEX [IX_MateriasModalidades_ModalidadId]
        ON [Catalogos].[MateriasModalidades]([ModalidadId]);

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId]=N'20260729160000_AgregarPlanesEstudioMaterias')
    INSERT INTO [dbo].[__EFMigrationsHistory]([MigrationId],[ProductVersion])
    VALUES(N'20260729160000_AgregarPlanesEstudioMaterias',N'10.0.10');

COMMIT TRANSACTION;
GO
