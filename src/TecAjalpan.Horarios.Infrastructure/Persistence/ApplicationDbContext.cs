using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TecAjalpan.Horarios.Domain.Common;
using TecAjalpan.Horarios.Domain.Entities;
using TecAjalpan.Horarios.Domain.Enums;
using TecAjalpan.Horarios.Infrastructure.Identity;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<UsuarioAplicacion>(options)
{
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<Modalidad> Modalidades => Set<Modalidad>();
    public DbSet<Reticula> Reticulas => Set<Reticula>();
    public DbSet<Materia> Materias => Set<Materia>();
    public DbSet<MateriaModalidad> MateriasModalidades => Set<MateriaModalidad>();
    public DbSet<Periodo> Periodos => Set<Periodo>();
    public DbSet<PeriodoCarrera> PeriodosCarreras => Set<PeriodoCarrera>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<OfertaMateria> OfertasMaterias => Set<OfertaMateria>();
    public DbSet<Docente> Docentes => Set<Docente>();
    public DbSet<DocenteCarrera> DocentesCarreras => Set<DocenteCarrera>();
    public DbSet<DisponibilidadDocente> DisponibilidadesDocentes => Set<DisponibilidadDocente>();
    public DbSet<DisponibilidadBloque> DisponibilidadesBloques => Set<DisponibilidadBloque>();
    public DbSet<JornadaDocente> JornadasDocentes => Set<JornadaDocente>();
    public DbSet<Espacio> Espacios => Set<Espacio>();
    public DbSet<DisponibilidadEspacio> DisponibilidadesEspacios => Set<DisponibilidadEspacio>();
    public DbSet<CargaAcademica> CargasAcademicas => Set<CargaAcademica>();
    public DbSet<ConfiguracionSabatina> ConfiguracionesSabatinas => Set<ConfiguracionSabatina>();
    public DbSet<ModuloSabatino> ModulosSabatinos => Set<ModuloSabatino>();
    public DbSet<ModuloMateria> ModulosMaterias => Set<ModuloMateria>();
    public DbSet<HorarioVersion> HorariosVersiones => Set<HorarioVersion>();
    public DbSet<SesionHorario> SesionesHorario => Set<SesionHorario>();
    public DbSet<PendienteGeneracion> PendientesGeneracion => Set<PendienteGeneracion>();
    public DbSet<EjecucionGenerador> EjecucionesGenerador => Set<EjecucionGenerador>();
    public DbSet<RevisionHorario> RevisionesHorario => Set<RevisionHorario>();
    public DbSet<AjusteManual> AjustesManuales => Set<AjusteManual>();
    public DbSet<UsuarioCarrera> UsuariosCarreras => Set<UsuarioCarrera>();
    public DbSet<Bitacora> Bitacora => Set<Bitacora>();
    public DbSet<ConfiguracionSistema> ConfiguracionSistema => Set<ConfiguracionSistema>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("Horarios");

        ConfigurarIdentity(builder);
        ConfigurarAuditoria(builder);
        ConfigurarAcademico(builder);
        ConfigurarRecursos(builder);
        ConfigurarHorario(builder);
        ConfigurarSabatino(builder);
        ConfigurarSemillas(builder);
    }

    private static void ConfigurarIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsuarioAplicacion>().ToTable("Usuarios", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UsuariosRoles", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UsuariosClaims", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UsuariosLogins", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RolesClaims", "Seguridad");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UsuariosTokens", "Seguridad");

        modelBuilder.Entity<UsuarioAplicacion>(entity =>
        {
            entity.Property(x => x.NombreCompleto).HasMaxLength(200);
            entity.HasQueryFilter(x => x.Activo);
        });

        modelBuilder.Entity<UsuarioCarrera>(entity =>
        {
            entity.ToTable("UsuariosCarreras", "Seguridad");
            entity.HasKey(x => new { x.UsuarioId, x.CarreraId });
            entity.HasOne<UsuarioAplicacion>()
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Carrera)
                .WithMany()
                .HasForeignKey(x => x.CarreraId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarAuditoria(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(EntidadAuditable).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(EntidadAuditable.RowVersion))
                .IsRowVersion()
                .IsConcurrencyToken();

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(
                    ExpressionHelper.CrearFiltroNoEliminado(entityType.ClrType));
        }

        modelBuilder.Entity<Bitacora>(entity =>
        {
            entity.ToTable("Bitacora", "Auditoria", table => table.IsTemporal());
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.Entidad).HasMaxLength(120);
            entity.Property(x => x.RegistroId).HasMaxLength(100);
            entity.Property(x => x.Accion).HasMaxLength(40);
            entity.Property(x => x.UsuarioId).HasMaxLength(450);
            entity.Property(x => x.ValoresAnteriores).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ValoresNuevos).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.Entidad, x.RegistroId, x.Fecha });
        });
    }

    private static void ConfigurarAcademico(ModelBuilder modelBuilder)
    {
        ConfigurarCatalogo<Carrera>(modelBuilder, "Carreras");
        ConfigurarCatalogo<Modalidad>(modelBuilder, "Modalidades");
        ConfigurarCatalogo<Reticula>(modelBuilder, "Reticulas");
        ConfigurarCatalogo<Materia>(modelBuilder, "Materias");
        ConfigurarCatalogo<Espacio>(modelBuilder, "Espacios");

        modelBuilder.Entity<Periodo>(entity =>
        {
            entity.ToTable("Periodos", "Academico");
            entity.Property(x => x.Nombre).HasMaxLength(120);
            entity.HasIndex(x => x.Nombre).IsUnique();
            entity.HasIndex(x => x.Estado)
                .IsUnique()
                .HasDatabaseName("UX_Periodos_UnicoActivo")
                .HasFilter("[Estado] = 2 AND [Eliminado] = 0");
        });
        modelBuilder.Entity<PeriodoCarrera>(entity =>
        {
            entity.ToTable("PeriodosCarreras", "Academico");
            entity.HasIndex(x => new { x.PeriodoId, x.CarreraId, x.ModalidadId }).IsUnique();
        });
        modelBuilder.Entity<Grupo>(entity =>
        {
            entity.ToTable("Grupos", "Academico");
            entity.Property(x => x.Clave).HasMaxLength(30);
            entity.Property(x => x.Nombre).HasMaxLength(120);
            entity.HasOne(x => x.EspacioBase).WithMany()
                .HasForeignKey(x => x.EspacioBaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.EspacioBaseId);
            entity.HasIndex(x => new { x.PeriodoCarreraId, x.Clave }).IsUnique();
        });
        modelBuilder.Entity<OfertaMateria>(entity =>
        {
            entity.ToTable("OfertasMaterias", "Academico");
            entity.HasIndex(x => new { x.GrupoId, x.MateriaId }).IsUnique();
        });
        modelBuilder.Entity<Reticula>(entity =>
        {
            entity.HasOne(x => x.Carrera).WithMany(x => x.Reticulas)
                .HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CarreraId, x.Clave })
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
        });
        modelBuilder.Entity<Materia>(entity =>
        {
            entity.HasOne(x => x.Reticula).WithMany(x => x.Materias)
                .HasForeignKey(x => x.ReticulaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ReticulaId, x.Clave })
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
        });
        modelBuilder.Entity<MateriaModalidad>(entity =>
        {
            entity.ToTable("MateriasModalidades", "Catalogos");
            entity.HasIndex(x => new { x.MateriaId, x.ModalidadId })
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
            entity.HasOne(x => x.Materia).WithMany(x => x.MateriasModalidades)
                .HasForeignKey(x => x.MateriaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Modalidad).WithMany()
                .HasForeignKey(x => x.ModalidadId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarRecursos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Docente>(entity =>
        {
            entity.ToTable("Docentes", "Recursos");
            entity.Property(x => x.NumeroTrabajador).HasMaxLength(30);
            entity.Property(x => x.Nombres).HasMaxLength(120);
            entity.Property(x => x.Apellidos).HasMaxLength(120);
            entity.Property(x => x.Correo).HasMaxLength(256);
            entity.HasIndex(x => x.NumeroTrabajador).IsUnique();
            entity.HasIndex(x => x.Correo).IsUnique();
        });
        modelBuilder.Entity<DocenteCarrera>(entity =>
        {
            entity.ToTable("DocentesCarreras", "Recursos");
            entity.HasIndex(x => new { x.DocenteId, x.CarreraId })
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
            entity.HasIndex(x => x.DocenteId)
                .IsUnique()
                .HasFilter("[EsPrincipal] = 1 AND [Eliminado] = 0");
            entity.HasOne(x => x.Docente)
                .WithMany(x => x.Carreras)
                .HasForeignKey(x => x.DocenteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Carrera)
                .WithMany()
                .HasForeignKey(x => x.CarreraId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DisponibilidadDocente>(entity =>
        {
            entity.ToTable("DisponibilidadesDocentes", "Recursos");
            entity.HasIndex(x => new { x.PeriodoId, x.DocenteId }).IsUnique();
        });
        modelBuilder.Entity<DisponibilidadBloque>(entity =>
        {
            entity.ToTable("DisponibilidadesBloques", "Recursos");
            entity.HasIndex(x => new { x.DisponibilidadDocenteId, x.Dia, x.Bloque }).IsUnique();
        });
        modelBuilder.Entity<JornadaDocente>(entity =>
        {
            entity.ToTable("JornadasDocentes", "Recursos");
            entity.HasIndex(x => new
                {
                    x.DisponibilidadDocenteId,
                    x.Dia,
                    x.EsSemanaSabatina
                })
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
            entity.HasOne(x => x.DisponibilidadDocente)
                .WithMany(x => x.Jornadas)
                .HasForeignKey(x => x.DisponibilidadDocenteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Espacio>(entity =>
        {
            entity.HasOne(x => x.Carrera).WithMany(x => x.Espacios)
                .HasForeignKey(x => x.CarreraId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CarreraId, x.Clave }).IsUnique();
            entity.Property(x => x.Tipo).HasMaxLength(60);
            entity.Property(x => x.Especialidad).HasMaxLength(120);
        });
        modelBuilder.Entity<DisponibilidadEspacio>(entity =>
        {
            entity.ToTable("DisponibilidadesEspacios", "Recursos");
            entity.HasIndex(x => new { x.PeriodoId, x.EspacioId, x.Dia, x.Bloque }).IsUnique();
        });
        modelBuilder.Entity<CargaAcademica>(entity =>
        {
            entity.ToTable("CargasAcademicas", "Operacion");
            entity.HasIndex(x => x.OfertaMateriaId)
                .IsUnique()
                .HasFilter("[Eliminado] = 0");
        });
    }

    private static void ConfigurarHorario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HorarioVersion>(entity =>
        {
            entity.ToTable("HorariosVersiones", "Horarios");

            entity.HasIndex(x => new
            {
                x.PeriodoId,
                x.PeriodoCarreraId,
                x.Numero
            }).IsUnique();

            entity.HasIndex(x => new
            {
                x.PeriodoId,
                x.PeriodoCarreraId
            })
            .IsUnique()
            .HasFilter(
                $"[Estado] = {(byte)EstadoHorario.Publicado} " +
                "AND [Eliminado] = 0");
        });

        modelBuilder.Entity<SesionHorario>(entity =>
        {
            entity.ToTable("SesionesHorario", "Horarios");

            entity.HasIndex(x => new
            {
                x.HorarioVersionId,
                x.Dia,
                x.Bloque,
                x.DocenteId
            }).IsUnique();

            entity.HasIndex(x => new
            {
                x.HorarioVersionId,
                x.Dia,
                x.Bloque,
                x.GrupoId
            }).IsUnique();

            entity.HasIndex(x => new
            {
                x.HorarioVersionId,
                x.Dia,
                x.Bloque,
                x.EspacioId
            }).IsUnique();

            entity.HasOne(x => x.HorarioVersion)
                .WithMany(x => x.Sesiones)
                .HasForeignKey(x => x.HorarioVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CargaAcademica)
                .WithMany()
                .HasForeignKey(x => x.CargaAcademicaId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PendienteGeneracion>(entity =>
        {
            entity.ToTable("PendientesGeneracion", "Horarios");

            entity.HasOne(x => x.HorarioVersion)
                .WithMany(x => x.Pendientes)
                .HasForeignKey(x => x.HorarioVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CargaAcademica)
                .WithMany()
                .HasForeignKey(x => x.CargaAcademicaId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EjecucionGenerador>()
            .ToTable("EjecucionesGenerador", "Horarios");

        modelBuilder.Entity<RevisionHorario>()
            .ToTable("RevisionesHorario", "Gobierno");

        modelBuilder.Entity<AjusteManual>()
            .ToTable("AjustesManuales", "Horarios");

        modelBuilder.Entity<ConfiguracionSistema>()
            .ToTable("ConfiguracionSistema", "Configuracion");
    }

    //private static void ConfigurarHorario(ModelBuilder modelBuilder)
    //{
    //    modelBuilder.Entity<HorarioVersion>(entity =>
    //    {
    //        entity.ToTable("HorariosVersiones", "Horarios");
    //        entity.HasIndex(x => new { x.PeriodoId, x.PeriodoCarreraId, x.Numero }).IsUnique();
    //        entity.HasIndex(x => new { x.PeriodoId, x.PeriodoCarreraId })
    //            .IsUnique()
    //            .HasFilter($"[Estado] = {(byte)EstadoHorario.Publicado} AND [Eliminado] = 0");
    //    });
    //    modelBuilder.Entity<SesionHorario>(entity =>
    //    {
    //        entity.ToTable("SesionesHorario", "Horarios");
    //        entity.HasIndex(x => new { x.HorarioVersionId, x.Dia, x.Bloque, x.DocenteId }).IsUnique();
    //        entity.HasIndex(x => new { x.HorarioVersionId, x.Dia, x.Bloque, x.GrupoId }).IsUnique();
    //        entity.HasIndex(x => new { x.HorarioVersionId, x.Dia, x.Bloque, x.EspacioId }).IsUnique();
    //    });
    //    modelBuilder.Entity<PendienteGeneracion>().ToTable("PendientesGeneracion", "Horarios");
    //    modelBuilder.Entity<EjecucionGenerador>().ToTable("EjecucionesGenerador", "Horarios");
    //    modelBuilder.Entity<RevisionHorario>().ToTable("RevisionesHorario", "Gobierno");
    //    modelBuilder.Entity<AjusteManual>().ToTable("AjustesManuales", "Horarios");
    //    modelBuilder.Entity<ConfiguracionSistema>().ToTable("ConfiguracionSistema", "Configuracion");
    //}

    private static void ConfigurarSabatino(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfiguracionSabatina>(entity =>
        {
            entity.ToTable("ConfiguracionesSabatinas", "Sabatino");
            entity.HasIndex(x => x.GrupoId).IsUnique();
            entity.HasOne(x => x.Grupo)
                .WithOne(x => x.ConfiguracionSabatina)
                .HasForeignKey<ConfiguracionSabatina>(x => x.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ModuloSabatino>(entity =>
        {
            entity.ToTable("ModulosSabatinos", "Sabatino");
            entity.HasIndex(x => new { x.ConfiguracionSabatinaId, x.Orden }).IsUnique();
        });
        modelBuilder.Entity<ModuloMateria>(entity =>
        {
            entity.ToTable("ModulosMaterias", "Sabatino");

            entity.HasIndex(x => new
            {
                x.ModuloSabatinoId,
                x.OfertaMateriaId
            }).IsUnique();

            entity.HasIndex(x => new
            {
                x.ModuloSabatinoId,
                x.Turno
            }).IsUnique();

            entity.HasOne(x => x.ModuloSabatino)
                .WithMany(x => x.Materias)
                .HasForeignKey(x => x.ModuloSabatinoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.OfertaMateria)
                .WithMany()
                .HasForeignKey(x => x.OfertaMateriaId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigurarSemillas(ModelBuilder modelBuilder)
    {
        var escolarizadaId = Guid.Parse("f5ecb763-fb0a-4a73-a897-fcc648661101");
        var sabatinaId = Guid.Parse("f5ecb763-fb0a-4a73-a897-fcc648661102");
        modelBuilder.Entity<Modalidad>().HasData(
            new
            {
                Id = escolarizadaId, Clave = "ESC", Nombre = "Escolarizada",
                Tipo = TipoModalidad.Escolarizada, Activo = true, Eliminado = false,
                UsuarioCrea = "sistema", FechaCrea = DateTime.UnixEpoch
            },
            new
            {
                Id = sabatinaId, Clave = "SAB", Nombre = "Sabatina",
                Tipo = TipoModalidad.Sabatina, Activo = true, Eliminado = false,
                UsuarioCrea = "sistema", FechaCrea = DateTime.UnixEpoch
            });
    }

    private static void ConfigurarCatalogo<T>(ModelBuilder modelBuilder, string tabla)
        where T : CatalogoAuditable
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.ToTable(tabla, "Catalogos");
            entity.Property(x => x.Clave).HasMaxLength(30);
            entity.Property(x => x.Nombre).HasMaxLength(200);
            if (typeof(T) != typeof(Reticula) && typeof(T) != typeof(Materia))
            {
                entity.HasIndex(x => x.Clave).IsUnique().HasFilter("[Eliminado] = 0");
            }
        });
    }
}
