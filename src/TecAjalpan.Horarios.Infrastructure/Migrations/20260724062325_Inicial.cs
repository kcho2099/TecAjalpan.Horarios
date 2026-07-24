using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TecAjalpan.Horarios.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Horarios");

            migrationBuilder.EnsureSchema(
                name: "Auditoria");

            migrationBuilder.EnsureSchema(
                name: "Operacion");

            migrationBuilder.EnsureSchema(
                name: "Catalogos");

            migrationBuilder.EnsureSchema(
                name: "Sabatino");

            migrationBuilder.EnsureSchema(
                name: "Configuracion");

            migrationBuilder.EnsureSchema(
                name: "Recursos");

            migrationBuilder.EnsureSchema(
                name: "Academico");

            migrationBuilder.EnsureSchema(
                name: "Gobierno");

            migrationBuilder.EnsureSchema(
                name: "Seguridad");

            migrationBuilder.CreateTable(
                name: "Bitacora",
                schema: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RegistroId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValoresAnteriores = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValoresNuevos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BitacoraHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auditoria")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Carreras",
                schema: "Catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carreras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionSistema",
                schema: "Configuracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreInstitucion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorSecundario = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RutaLogo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InicioEscolarizado = table.Column<byte>(type: "tinyint", nullable: false),
                    FinEscolarizado = table.Column<byte>(type: "tinyint", nullable: false),
                    DuracionBloqueMinutos = table.Column<byte>(type: "tinyint", nullable: false),
                    MaximoConsecutivasMateria = table.Column<byte>(type: "tinyint", nullable: false),
                    MaximoHorasDocenteDia = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Docentes",
                schema: "Recursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroTrabajador = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    HorasPermanenciaSemanal = table.Column<byte>(type: "tinyint", nullable: false),
                    CargaMaximaSemanal = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Docentes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EjecucionesGenerador",
                schema: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoCarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TiempoLimiteSegundos = table.Column<int>(type: "int", nullable: false),
                    HorasSolicitadas = table.Column<int>(type: "int", nullable: false),
                    HorasProgramadas = table.Column<int>(type: "int", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionesGenerador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modalidades",
                schema: "Catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modalidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Periodos",
                schema: "Academico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Semanas = table.Column<byte>(type: "tinyint", nullable: false),
                    SemestresPares = table.Column<bool>(type: "bit", nullable: false),
                    PermitirExcepcionSemestre = table.Column<bool>(type: "bit", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periodos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "Seguridad",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                schema: "Seguridad",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DebeCambiarContrasena = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Espacios",
                schema: "Catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Capacidad = table.Column<short>(type: "smallint", nullable: true),
                    Especialidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Espacios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Espacios_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalSchema: "Catalogos",
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reticulas",
                schema: "Catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    FinVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reticulas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reticulas_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalSchema: "Catalogos",
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisponibilidadesDocentes",
                schema: "Recursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Validada = table.Column<bool>(type: "bit", nullable: false),
                    FechaValidacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsuarioValida = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisponibilidadesDocentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisponibilidadesDocentes_Docentes_DocenteId",
                        column: x => x.DocenteId,
                        principalSchema: "Recursos",
                        principalTable: "Docentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisponibilidadesDocentes_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalSchema: "Academico",
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosCarreras",
                schema: "Academico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModalidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosCarreras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosCarreras_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalSchema: "Catalogos",
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodosCarreras_Modalidades_ModalidadId",
                        column: x => x.ModalidadId,
                        principalSchema: "Catalogos",
                        principalTable: "Modalidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodosCarreras_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalSchema: "Academico",
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolesClaims",
                schema: "Seguridad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolesClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Seguridad",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosCarreras",
                schema: "Seguridad",
                columns: table => new
                {
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosCarreras", x => new { x.UsuarioId, x.CarreraId });
                    table.ForeignKey(
                        name: "FK_UsuariosCarreras_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalSchema: "Catalogos",
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosCarreras_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "Seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosClaims",
                schema: "Seguridad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosClaims_Usuarios_UserId",
                        column: x => x.UserId,
                        principalSchema: "Seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosLogins",
                schema: "Seguridad",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UsuariosLogins_Usuarios_UserId",
                        column: x => x.UserId,
                        principalSchema: "Seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosRoles",
                schema: "Seguridad",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Seguridad",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Usuarios_UserId",
                        column: x => x.UserId,
                        principalSchema: "Seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosTokens",
                schema: "Seguridad",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UsuariosTokens_Usuarios_UserId",
                        column: x => x.UserId,
                        principalSchema: "Seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisponibilidadesEspacios",
                schema: "Recursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dia = table.Column<byte>(type: "tinyint", nullable: false),
                    Bloque = table.Column<byte>(type: "tinyint", nullable: false),
                    Disponible = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisponibilidadesEspacios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisponibilidadesEspacios_Espacios_EspacioId",
                        column: x => x.EspacioId,
                        principalSchema: "Catalogos",
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisponibilidadesEspacios_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalSchema: "Academico",
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materias",
                schema: "Catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReticulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semestre = table.Column<byte>(type: "tinyint", nullable: false),
                    Creditos = table.Column<byte>(type: "tinyint", nullable: false),
                    HorasSemanales = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materias_Reticulas_ReticulaId",
                        column: x => x.ReticulaId,
                        principalSchema: "Catalogos",
                        principalTable: "Reticulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisponibilidadesBloques",
                schema: "Recursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisponibilidadDocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dia = table.Column<byte>(type: "tinyint", nullable: false),
                    Bloque = table.Column<byte>(type: "tinyint", nullable: false),
                    Disponible = table.Column<bool>(type: "bit", nullable: false),
                    Preferente = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisponibilidadesBloques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisponibilidadesBloques_DisponibilidadesDocentes_DisponibilidadDocenteId",
                        column: x => x.DisponibilidadDocenteId,
                        principalSchema: "Recursos",
                        principalTable: "DisponibilidadesDocentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                schema: "Academico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoCarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semestre = table.Column<byte>(type: "tinyint", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grupos_PeriodosCarreras_PeriodoCarreraId",
                        column: x => x.PeriodoCarreraId,
                        principalSchema: "Academico",
                        principalTable: "PeriodosCarreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HorariosVersiones",
                schema: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodoCarreraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsuarioPublica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosVersiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorariosVersiones_PeriodosCarreras_PeriodoCarreraId",
                        column: x => x.PeriodoCarreraId,
                        principalSchema: "Academico",
                        principalTable: "PeriodosCarreras",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HorariosVersiones_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalSchema: "Academico",
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesSabatinas",
                schema: "Sabatino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Validada = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesSabatinas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesSabatinas_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalSchema: "Academico",
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfertasMaterias",
                schema: "Academico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MateriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorasRequeridas = table.Column<byte>(type: "tinyint", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfertasMaterias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfertasMaterias_Grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalSchema: "Academico",
                        principalTable: "Grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OfertasMaterias_Materias_MateriaId",
                        column: x => x.MateriaId,
                        principalSchema: "Catalogos",
                        principalTable: "Materias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionesHorario",
                schema: "Gobierno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorarioVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionesHorario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionesHorario_HorariosVersiones_HorarioVersionId",
                        column: x => x.HorarioVersionId,
                        principalSchema: "Horarios",
                        principalTable: "HorariosVersiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulosSabatinos",
                schema: "Sabatino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfiguracionSabatinaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Semanas = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosSabatinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulosSabatinos_ConfiguracionesSabatinas_ConfiguracionSabatinaId",
                        column: x => x.ConfiguracionSabatinaId,
                        principalSchema: "Sabatino",
                        principalTable: "ConfiguracionesSabatinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CargasAcademicas",
                schema: "Operacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfertaMateriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsuarioAutoriza = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasAcademicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CargasAcademicas_Docentes_DocenteId",
                        column: x => x.DocenteId,
                        principalSchema: "Recursos",
                        principalTable: "Docentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CargasAcademicas_OfertasMaterias_OfertaMateriaId",
                        column: x => x.OfertaMateriaId,
                        principalSchema: "Academico",
                        principalTable: "OfertasMaterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulosMaterias",
                schema: "Sabatino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuloSabatinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfertaMateriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Turno = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosMaterias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulosMaterias_ModulosSabatinos_ModuloSabatinoId",
                        column: x => x.ModuloSabatinoId,
                        principalSchema: "Sabatino",
                        principalTable: "ModulosSabatinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModulosMaterias_OfertasMaterias_OfertaMateriaId",
                        column: x => x.OfertaMateriaId,
                        principalSchema: "Academico",
                        principalTable: "OfertasMaterias",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PendientesGeneracion",
                schema: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorarioVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CargaAcademicaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorasPendientes = table.Column<byte>(type: "tinyint", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendientesGeneracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendientesGeneracion_CargasAcademicas_CargaAcademicaId",
                        column: x => x.CargaAcademicaId,
                        principalSchema: "Operacion",
                        principalTable: "CargasAcademicas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendientesGeneracion_HorariosVersiones_HorarioVersionId",
                        column: x => x.HorarioVersionId,
                        principalSchema: "Horarios",
                        principalTable: "HorariosVersiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SesionesHorario",
                schema: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorarioVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CargaAcademicaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspacioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dia = table.Column<byte>(type: "tinyint", nullable: false),
                    Bloque = table.Column<byte>(type: "tinyint", nullable: false),
                    DuracionBloques = table.Column<byte>(type: "tinyint", nullable: false),
                    Origen = table.Column<byte>(type: "tinyint", nullable: false),
                    FijadaParaRegeneracion = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesHorario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesHorario_CargasAcademicas_CargaAcademicaId",
                        column: x => x.CargaAcademicaId,
                        principalSchema: "Operacion",
                        principalTable: "CargasAcademicas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SesionesHorario_Espacios_EspacioId",
                        column: x => x.EspacioId,
                        principalSchema: "Catalogos",
                        principalTable: "Espacios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SesionesHorario_HorariosVersiones_HorarioVersionId",
                        column: x => x.HorarioVersionId,
                        principalSchema: "Horarios",
                        principalTable: "HorariosVersiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AjustesManuales",
                schema: "Horarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SesionHorarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValoresAnteriores = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValoresNuevos = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsuarioCrea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCrea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioModifica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    UsuarioElimina = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaElimina = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesManuales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AjustesManuales_SesionesHorario_SesionHorarioId",
                        column: x => x.SesionHorarioId,
                        principalSchema: "Horarios",
                        principalTable: "SesionesHorario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "Catalogos",
                table: "Modalidades",
                columns: new[] { "Id", "Activo", "Clave", "Eliminado", "FechaCrea", "FechaElimina", "FechaModifica", "Nombre", "Tipo", "UsuarioCrea", "UsuarioElimina", "UsuarioModifica" },
                values: new object[,]
                {
                    { new Guid("f5ecb763-fb0a-4a73-a897-fcc648661101"), true, "ESC", false, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Escolarizada", (byte)1, "sistema", null, null },
                    { new Guid("f5ecb763-fb0a-4a73-a897-fcc648661102"), true, "SAB", false, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Sabatina", (byte)2, "sistema", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AjustesManuales_SesionHorarioId",
                schema: "Horarios",
                table: "AjustesManuales",
                column: "SesionHorarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Entidad_RegistroId_Fecha",
                schema: "Auditoria",
                table: "Bitacora",
                columns: new[] { "Entidad", "RegistroId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_CargasAcademicas_DocenteId",
                schema: "Operacion",
                table: "CargasAcademicas",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_CargasAcademicas_OfertaMateriaId",
                schema: "Operacion",
                table: "CargasAcademicas",
                column: "OfertaMateriaId",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Carreras_Clave",
                schema: "Catalogos",
                table: "Carreras",
                column: "Clave",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesSabatinas_GrupoId",
                schema: "Sabatino",
                table: "ConfiguracionesSabatinas",
                column: "GrupoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisponibilidadesBloques_DisponibilidadDocenteId_Dia_Bloque",
                schema: "Recursos",
                table: "DisponibilidadesBloques",
                columns: new[] { "DisponibilidadDocenteId", "Dia", "Bloque" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisponibilidadesDocentes_DocenteId",
                schema: "Recursos",
                table: "DisponibilidadesDocentes",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_DisponibilidadesDocentes_PeriodoId_DocenteId",
                schema: "Recursos",
                table: "DisponibilidadesDocentes",
                columns: new[] { "PeriodoId", "DocenteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisponibilidadesEspacios_EspacioId",
                schema: "Recursos",
                table: "DisponibilidadesEspacios",
                column: "EspacioId");

            migrationBuilder.CreateIndex(
                name: "IX_DisponibilidadesEspacios_PeriodoId_EspacioId_Dia_Bloque",
                schema: "Recursos",
                table: "DisponibilidadesEspacios",
                columns: new[] { "PeriodoId", "EspacioId", "Dia", "Bloque" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Docentes_Correo",
                schema: "Recursos",
                table: "Docentes",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Docentes_NumeroTrabajador",
                schema: "Recursos",
                table: "Docentes",
                column: "NumeroTrabajador",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Espacios_CarreraId_Clave",
                schema: "Catalogos",
                table: "Espacios",
                columns: new[] { "CarreraId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Espacios_Clave",
                schema: "Catalogos",
                table: "Espacios",
                column: "Clave",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Grupos_PeriodoCarreraId_Clave",
                schema: "Academico",
                table: "Grupos",
                columns: new[] { "PeriodoCarreraId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HorariosVersiones_PeriodoCarreraId",
                schema: "Horarios",
                table: "HorariosVersiones",
                column: "PeriodoCarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_HorariosVersiones_PeriodoId_PeriodoCarreraId",
                schema: "Horarios",
                table: "HorariosVersiones",
                columns: new[] { "PeriodoId", "PeriodoCarreraId" },
                unique: true,
                filter: "[Estado] = 4 AND [Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HorariosVersiones_PeriodoId_PeriodoCarreraId_Numero",
                schema: "Horarios",
                table: "HorariosVersiones",
                columns: new[] { "PeriodoId", "PeriodoCarreraId", "Numero" },
                unique: true,
                filter: "[PeriodoCarreraId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Materias_Clave",
                schema: "Catalogos",
                table: "Materias",
                column: "Clave",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Materias_ReticulaId",
                schema: "Catalogos",
                table: "Materias",
                column: "ReticulaId");

            migrationBuilder.CreateIndex(
                name: "IX_Modalidades_Clave",
                schema: "Catalogos",
                table: "Modalidades",
                column: "Clave",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ModulosMaterias_ModuloSabatinoId_OfertaMateriaId",
                schema: "Sabatino",
                table: "ModulosMaterias",
                columns: new[] { "ModuloSabatinoId", "OfertaMateriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModulosMaterias_ModuloSabatinoId_Turno",
                schema: "Sabatino",
                table: "ModulosMaterias",
                columns: new[] { "ModuloSabatinoId", "Turno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModulosMaterias_OfertaMateriaId",
                schema: "Sabatino",
                table: "ModulosMaterias",
                column: "OfertaMateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ModulosSabatinos_ConfiguracionSabatinaId_Orden",
                schema: "Sabatino",
                table: "ModulosSabatinos",
                columns: new[] { "ConfiguracionSabatinaId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfertasMaterias_GrupoId_MateriaId",
                schema: "Academico",
                table: "OfertasMaterias",
                columns: new[] { "GrupoId", "MateriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfertasMaterias_MateriaId",
                schema: "Academico",
                table: "OfertasMaterias",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesGeneracion_CargaAcademicaId",
                schema: "Horarios",
                table: "PendientesGeneracion",
                column: "CargaAcademicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesGeneracion_HorarioVersionId",
                schema: "Horarios",
                table: "PendientesGeneracion",
                column: "HorarioVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Periodos_Nombre",
                schema: "Academico",
                table: "Periodos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosCarreras_CarreraId",
                schema: "Academico",
                table: "PeriodosCarreras",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosCarreras_ModalidadId",
                schema: "Academico",
                table: "PeriodosCarreras",
                column: "ModalidadId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosCarreras_PeriodoId_CarreraId_ModalidadId",
                schema: "Academico",
                table: "PeriodosCarreras",
                columns: new[] { "PeriodoId", "CarreraId", "ModalidadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reticulas_CarreraId",
                schema: "Catalogos",
                table: "Reticulas",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Reticulas_Clave",
                schema: "Catalogos",
                table: "Reticulas",
                column: "Clave",
                unique: true,
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionesHorario_HorarioVersionId",
                schema: "Gobierno",
                table: "RevisionesHorario",
                column: "HorarioVersionId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "Seguridad",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolesClaims_RoleId",
                schema: "Seguridad",
                table: "RolesClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesHorario_CargaAcademicaId",
                schema: "Horarios",
                table: "SesionesHorario",
                column: "CargaAcademicaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesHorario_EspacioId",
                schema: "Horarios",
                table: "SesionesHorario",
                column: "EspacioId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_DocenteId",
                schema: "Horarios",
                table: "SesionesHorario",
                columns: new[] { "HorarioVersionId", "Dia", "Bloque", "DocenteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_EspacioId",
                schema: "Horarios",
                table: "SesionesHorario",
                columns: new[] { "HorarioVersionId", "Dia", "Bloque", "EspacioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesHorario_HorarioVersionId_Dia_Bloque_GrupoId",
                schema: "Horarios",
                table: "SesionesHorario",
                columns: new[] { "HorarioVersionId", "Dia", "Bloque", "GrupoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "Seguridad",
                table: "Usuarios",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "Seguridad",
                table: "Usuarios",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCarreras_CarreraId",
                schema: "Seguridad",
                table: "UsuariosCarreras",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosClaims_UserId",
                schema: "Seguridad",
                table: "UsuariosClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosLogins_UserId",
                schema: "Seguridad",
                table: "UsuariosLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosRoles_RoleId",
                schema: "Seguridad",
                table: "UsuariosRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AjustesManuales",
                schema: "Horarios");

            migrationBuilder.DropTable(
                name: "Bitacora",
                schema: "Auditoria")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BitacoraHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auditoria")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "ConfiguracionSistema",
                schema: "Configuracion");

            migrationBuilder.DropTable(
                name: "DisponibilidadesBloques",
                schema: "Recursos");

            migrationBuilder.DropTable(
                name: "DisponibilidadesEspacios",
                schema: "Recursos");

            migrationBuilder.DropTable(
                name: "EjecucionesGenerador",
                schema: "Horarios");

            migrationBuilder.DropTable(
                name: "ModulosMaterias",
                schema: "Sabatino");

            migrationBuilder.DropTable(
                name: "PendientesGeneracion",
                schema: "Horarios");

            migrationBuilder.DropTable(
                name: "RevisionesHorario",
                schema: "Gobierno");

            migrationBuilder.DropTable(
                name: "RolesClaims",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "UsuariosCarreras",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "UsuariosClaims",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "UsuariosLogins",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "UsuariosRoles",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "UsuariosTokens",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "SesionesHorario",
                schema: "Horarios");

            migrationBuilder.DropTable(
                name: "DisponibilidadesDocentes",
                schema: "Recursos");

            migrationBuilder.DropTable(
                name: "ModulosSabatinos",
                schema: "Sabatino");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "Usuarios",
                schema: "Seguridad");

            migrationBuilder.DropTable(
                name: "CargasAcademicas",
                schema: "Operacion");

            migrationBuilder.DropTable(
                name: "Espacios",
                schema: "Catalogos");

            migrationBuilder.DropTable(
                name: "HorariosVersiones",
                schema: "Horarios");

            migrationBuilder.DropTable(
                name: "ConfiguracionesSabatinas",
                schema: "Sabatino");

            migrationBuilder.DropTable(
                name: "Docentes",
                schema: "Recursos");

            migrationBuilder.DropTable(
                name: "OfertasMaterias",
                schema: "Academico");

            migrationBuilder.DropTable(
                name: "Grupos",
                schema: "Academico");

            migrationBuilder.DropTable(
                name: "Materias",
                schema: "Catalogos");

            migrationBuilder.DropTable(
                name: "PeriodosCarreras",
                schema: "Academico");

            migrationBuilder.DropTable(
                name: "Reticulas",
                schema: "Catalogos");

            migrationBuilder.DropTable(
                name: "Modalidades",
                schema: "Catalogos");

            migrationBuilder.DropTable(
                name: "Periodos",
                schema: "Academico");

            migrationBuilder.DropTable(
                name: "Carreras",
                schema: "Catalogos");
        }
    }
}
