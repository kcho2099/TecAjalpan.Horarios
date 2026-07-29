using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TecAjalpan.Horarios.Infrastructure.Persistence;

#nullable disable

namespace TecAjalpan.Horarios.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729190000_RepararMateriasModalidades")]
public partial class RepararMateriasModalidades : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
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
                    CONSTRAINT [FK_MateriasModalidades_Materias_MateriaId]
                        FOREIGN KEY ([MateriaId]) REFERENCES [Catalogos].[Materias]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_MateriasModalidades_Modalidades_ModalidadId]
                        FOREIGN KEY ([ModalidadId]) REFERENCES [Catalogos].[Modalidades]([Id])
                );
            END;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_MateriasModalidades_MateriaId_ModalidadId'
                  AND [object_id] = OBJECT_ID(N'[Catalogos].[MateriasModalidades]'))
                CREATE UNIQUE INDEX [IX_MateriasModalidades_MateriaId_ModalidadId]
                    ON [Catalogos].[MateriasModalidades]([MateriaId], [ModalidadId])
                    WHERE [Eliminado] = 0;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_MateriasModalidades_ModalidadId'
                  AND [object_id] = OBJECT_ID(N'[Catalogos].[MateriasModalidades]'))
                CREATE INDEX [IX_MateriasModalidades_ModalidadId]
                    ON [Catalogos].[MateriasModalidades]([ModalidadId]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reparación no destructiva: no se elimina una tabla que puede contener relaciones.
    }
}
