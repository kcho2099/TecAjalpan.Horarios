SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH(
    N'Recursos.JornadasDocentes',
    N'EsSemanaSabatina'
) IS NULL
BEGIN
    ALTER TABLE [Recursos].[JornadasDocentes]
    ADD [EsSemanaSabatina] bit NOT NULL
        CONSTRAINT [DF_JornadasDocentes_EsSemanaSabatina]
        DEFAULT (0);
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] =
        N'IX_JornadasDocentes_DisponibilidadDocenteId_Dia'
      AND [object_id] =
        OBJECT_ID(N'[Recursos].[JornadasDocentes]')
)
BEGIN
    DROP INDEX
        [IX_JornadasDocentes_DisponibilidadDocenteId_Dia]
        ON [Recursos].[JornadasDocentes];
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] =
        N'IX_JornadasDocentes_DisponibilidadDocenteId_Dia_EsSemanaSabatina'
      AND [object_id] =
        OBJECT_ID(N'[Recursos].[JornadasDocentes]')
)
BEGIN
    CREATE UNIQUE INDEX
        [IX_JornadasDocentes_DisponibilidadDocenteId_Dia_EsSemanaSabatina]
        ON [Recursos].[JornadasDocentes]
        (
            [DisponibilidadDocenteId],
            [Dia],
            [EsSemanaSabatina]
        )
        WHERE [Eliminado] = 0;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] =
        N'20260725050000_AgregarPerfilSabatinoJornadaDocente'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory]
        ([MigrationId], [ProductVersion])
    VALUES
        (
            N'20260725050000_AgregarPerfilSabatinoJornadaDocente',
            N'10.0.10'
        );
END;
GO

COMMIT TRANSACTION;
GO
