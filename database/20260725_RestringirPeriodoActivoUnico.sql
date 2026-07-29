SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

UPDATE [Academico].[Periodos]
SET [Estado] = 3
WHERE [Estado] = 2
  AND [Eliminado] = 0
  AND [FechaFin] < CONVERT(date, GETDATE());
GO

;WITH [PeriodosActivos] AS
(
    SELECT
        [Id],
        ROW_NUMBER() OVER (
            ORDER BY [FechaInicio] DESC, [FechaCrea] DESC, [Id]
        ) AS [Numero]
    FROM [Academico].[Periodos]
    WHERE [Estado] = 2
      AND [Eliminado] = 0
)
UPDATE [Periodo]
SET [Estado] = 3
FROM [Academico].[Periodos] AS [Periodo]
INNER JOIN [PeriodosActivos] AS [Activo]
    ON [Activo].[Id] = [Periodo].[Id]
WHERE [Activo].[Numero] > 1;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_Periodos_UnicoActivo'
      AND [object_id] = OBJECT_ID(N'[Academico].[Periodos]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_Periodos_UnicoActivo]
        ON [Academico].[Periodos] ([Estado])
        WHERE [Estado] = 2 AND [Eliminado] = 0;
END;
GO

COMMIT TRANSACTION;
GO
