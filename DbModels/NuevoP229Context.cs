using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PasadorScript.DbModels;

public partial class NuevoP229Context : DbContext
{
    public NuevoP229Context(DbContextOptions<NuevoP229Context> options)
        : base(options)
    {
    }

    public virtual DbSet<Accione> Acciones { get; set; }

    public virtual DbSet<AdGroupMapping> AdGroupMappings { get; set; }

    public virtual DbSet<BuildsGx> BuildsGxes { get; set; }

    public virtual DbSet<Entorno> Entornos { get; set; }

    public virtual DbSet<EnviosDlya> EnviosDlyas { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<Menue> Menues { get; set; }

    public virtual DbSet<Pat> Pats { get; set; }

    public virtual DbSet<PullRequest> PullRequests { get; set; }

    public virtual DbSet<PullRequestsBuild> PullRequestsBuilds { get; set; }

    public virtual DbSet<PullRequestsEnviosDlya> PullRequestsEnviosDlyas { get; set; }

    public virtual DbSet<RolAccion> RolAccions { get; set; }

    public virtual DbSet<RolesMap> RolesMaps { get; set; }

    public virtual DbSet<Script> Scripts { get; set; }

    public virtual DbSet<SubMenue> SubMenues { get; set; }

    public virtual DbSet<TiposCambio> TiposCambios { get; set; }

    public virtual DbSet<TurnosModificacion> TurnosModificacions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Accione>(entity =>
        {
            entity.HasKey(e => e.AccionId).HasName("PK__Acciones__A60CAFC78A0F13DA");

            entity.Property(e => e.Categoria).HasMaxLength(50);
            entity.Property(e => e.Descripcion).HasMaxLength(255);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Orden).HasDefaultValue(0);
        });

        modelBuilder.Entity<AdGroupMapping>(entity =>
        {
            entity.HasKey(e => e.GroupGuid);

            entity.ToTable("AdGroupMapping");

            entity.Property(e => e.GroupGuid)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("groupGuid");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())", "DF_AdGroupMapping_createdAt")
                .HasColumnType("datetime")
                .HasColumnName("createdAt");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("description");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.GroupName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("groupName");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "DF_AdGroupMapping_isActive")
                .HasColumnName("isActive");
        });

        modelBuilder.Entity<BuildsGx>(entity =>
        {
            entity.HasKey(e => e.IdBuildGx);

            entity.ToTable("BuildsGX");

            entity.Property(e => e.IdBuildGx).HasColumnName("idBuildGx");
            entity.Property(e => e.AzureProjectType)
                .HasMaxLength(50)
                .HasDefaultValue("GeneXus");
            entity.Property(e => e.BranchName)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("branchName");
            entity.Property(e => e.CommitListJson).HasColumnName("commitListJson");
            entity.Property(e => e.Deleted).HasColumnName("deleted");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(4000)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())", "DF_Table_1_fechaHora")
                .HasColumnType("datetime")
                .HasColumnName("fechaCreacion");
            entity.Property(e => e.IdPr).HasColumnName("idPr");
            entity.Property(e => e.IdTipoCambio).HasColumnName("idTipoCambio");
            entity.Property(e => e.NroIncidente).HasColumnName("nroIncidente");
            entity.Property(e => e.RepositoryId)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("repositoryId");
            entity.Property(e => e.Usuario)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("usuario");

            entity.HasOne(d => d.IdTipoCambioNavigation).WithMany(p => p.BuildsGxes)
                .HasForeignKey(d => d.IdTipoCambio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BuildsGX_TiposCambios");
            entity.Property(e => e.IsHotfix).HasColumnName("isHotfix");
            entity.Property(e => e.SinPrueba).HasColumnName("sinPrueba");
            entity.Property(e => e.TareasJson).HasColumnName("tareasJson");
        });

        modelBuilder.Entity<Entorno>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Entornos__3214EC070306EDCA");

            entity.HasIndex(e => e.Activo, "IX_Entornos_Activo");

            entity.HasIndex(e => e.Nombre, "UQ_Entorno_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Icono).HasMaxLength(50);
            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<EnviosDlya>(entity =>
        {
            entity.HasKey(e => e.IdEnvioDlya);

            entity.ToTable("EnviosDlya");

            entity.Property(e => e.IdEnvioDlya).HasColumnName("idEnvioDlya");
            entity.Property(e => e.AzureProjectType)
                .HasMaxLength(50)
                .HasDefaultValue("Genexus");
            entity.Property(e => e.BranchName)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("branchName");
            entity.Property(e => e.CommitListJson).HasColumnName("commitListJson");
            entity.Property(e => e.Deleted).HasColumnName("deleted");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(4000)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())", "DF_EnviosDlya_fechaCreacion")
                .HasColumnType("datetime")
                .HasColumnName("fechaCreacion");
            entity.Property(e => e.IdPr).HasColumnName("idPr");
            entity.Property(e => e.IdTipoCambio).HasColumnName("idTipoCambio");
            entity.Property(e => e.NroIncidente).HasColumnName("nroIncidente");
            entity.Property(e => e.RepositoryId)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("repositoryId");
            entity.Property(e => e.Usuario)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("usuario");
            entity.Property(e => e.IsHotfix).HasColumnName("isHotfix");
            entity.Property(e => e.SinPrueba).HasColumnName("sinPrueba");
            entity.Property(e => e.TareasJson).HasColumnName("tareasJson");

            entity.HasOne(d => d.IdTipoCambioNavigation).WithMany(p => p.EnviosDlyas)
                .HasForeignKey(d => d.IdTipoCambio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EnviosDlya_EnviosDlya");
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.Property(e => e.RequestId).HasMaxLength(100);
            entity.Property(e => e.Service).HasMaxLength(100);
            entity.Property(e => e.TimeStamp).HasColumnType("datetime");
            entity.Property(e => e.UserId).HasMaxLength(100);
        });

        modelBuilder.Entity<Menue>(entity =>
        {
            entity.HasKey(e => e.IdMenu);

            entity.Property(e => e.IdMenu).HasColumnName("idMenu");
            entity.Property(e => e.AspAction)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("aspAction");
            entity.Property(e => e.AspController)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("aspController");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.IClass)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("iClass");
            entity.Property(e => e.IdPerfil).HasColumnName("idPerfil");
        });

        modelBuilder.Entity<Pat>(entity =>
        {
            entity.HasKey(e => e.Username);

            entity.Property(e => e.Username)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("username");
            entity.Property(e => e.FechaCreacion)
                .HasColumnType("datetime")
                .HasColumnName("fechaCreacion");
            entity.Property(e => e.Pat1)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("pat");
        });

        modelBuilder.Entity<PullRequest>(entity =>
        {
            entity.HasKey(e => new { e.IdScript, e.IdPr });

            entity.Property(e => e.IdScript).HasColumnName("idScript");
            entity.Property(e => e.IdPr).HasColumnName("idPR");
            entity.Property(e => e.QaStage).HasColumnName("qaStage");

            entity.HasOne(d => d.IdScriptNavigation).WithMany(p => p.PullRequests)
                .HasForeignKey(d => d.IdScript)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PullRequests_Scripts");
        });

        modelBuilder.Entity<PullRequestsBuild>(entity =>
        {
            entity.HasKey(e => new { e.IdBuild, e.IdPr });

            entity.Property(e => e.IdBuild).HasColumnName("idBuild");
            entity.Property(e => e.IdPr).HasColumnName("idPr");

            entity.HasOne(d => d.IdBuildNavigation).WithMany(p => p.PullRequestsBuilds)
                .HasForeignKey(d => d.IdBuild)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PullRequestsBuilds_BuildsGX");
        });

        modelBuilder.Entity<PullRequestsEnviosDlya>(entity =>
        {
            entity.HasKey(e => new { e.IdEnvioDlya, e.IdPr });

            entity.ToTable("PullRequestsEnviosDlya");

            entity.Property(e => e.IdEnvioDlya).HasColumnName("idEnvioDlya");
            entity.Property(e => e.IdPr).HasColumnName("idPr");

            entity.HasOne(d => d.IdEnvioDlyaNavigation).WithMany(p => p.PullRequestsEnviosDlyas)
                .HasForeignKey(d => d.IdEnvioDlya)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PullRequestsEnviosDlya_EnviosDlya");
        });

        modelBuilder.Entity<RolAccion>(entity =>
        {
            entity.HasKey(e => e.RolAccionId).HasName("PK__RolAccio__D9F38BBADE68CB08");

            entity.ToTable("RolAccion");

            entity.HasIndex(e => new { e.GroupGuid, e.AccionId }, "UQ_RolAccion").IsUnique();

            entity.Property(e => e.AsignadoPor).HasMaxLength(100);
            entity.Property(e => e.FechaAsignacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.GroupGuid)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Accion).WithMany(p => p.RolAccions)
                .HasForeignKey(d => d.AccionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RolAccion_Acciones");

            entity.HasOne(d => d.Group).WithMany(p => p.RolAccions)
                .HasForeignKey(d => d.GroupGuid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RolAccion_AdGroupMapping");
        });

        modelBuilder.Entity<RolesMap>(entity =>
        {
            entity.HasKey(e => e.IdRol);

            entity.ToTable("RolesMap");

            entity.Property(e => e.IdRol)
                .ValueGeneratedNever()
                .HasColumnName("idRol");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("descripcion");
        });

        modelBuilder.Entity<Script>(entity =>
        {
            entity.HasKey(e => e.IdScript);

            entity.Property(e => e.IdScript).HasColumnName("idScript");
            entity.Property(e => e.Aplicacion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("aplicacion");
            entity.Property(e => e.AzureProjectType)
                .HasMaxLength(50)
                .HasDefaultValue("Default");
            entity.Property(e => e.BranchNameDesa)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("branchNameDesa");
            entity.Property(e => e.BranchNamePr)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("branchNamePR");
            entity.Property(e => e.BranchNameQa)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("branchNameQA");
            entity.Property(e => e.CommitId)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("commitId");
            entity.Property(e => e.Deleted).HasColumnName("deleted");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(4000)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())", "DF_Scripts_fechaCreacion")
                .HasColumnType("datetime")
                .HasColumnName("fechaCreacion");
            entity.Property(e => e.Files).HasColumnName("files");
            entity.Property(e => e.IdTipoCambio).HasColumnName("idTipoCambio");
            entity.Property(e => e.NroIncidente).HasColumnName("nroIncidente");
            entity.Property(e => e.PullRequestIdQa)
                .HasDefaultValue(0, "DF_Scripts_pullRequestId")
                .HasColumnName("pullRequestIdQA");
            entity.Property(e => e.PullRequestIdprd).HasColumnName("pullRequestIDPRD");
            entity.Property(e => e.Usuario)
                .HasMaxLength(80)
                .IsUnicode(false)
                .HasColumnName("usuario");
            entity.Property(e => e.IsHotfix).HasColumnName("isHotfix");
            entity.Property(e => e.SinPrueba).HasColumnName("sinPrueba");
            entity.Property(e => e.TareasJson).HasColumnName("tareasJson");

            entity.HasOne(d => d.IdTipoCambioNavigation).WithMany(p => p.Scripts)
                .HasForeignKey(d => d.IdTipoCambio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Scripts_TiposCambios");
        });

        modelBuilder.Entity<SubMenue>(entity =>
        {
            entity.HasKey(e => e.IdSubmenu);

            entity.Property(e => e.IdSubmenu).HasColumnName("idSubmenu");
            entity.Property(e => e.AspAction)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("aspAction");
            entity.Property(e => e.AspController)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("aspController");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.IdMenu).HasColumnName("idMenu");
            entity.Property(e => e.IdPermiso)
                .HasMaxLength(10)
                .IsFixedLength()
                .HasColumnName("idPermiso");

            entity.HasOne(d => d.IdMenuNavigation).WithMany(p => p.SubMenues)
                .HasForeignKey(d => d.IdMenu)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SubMenues_SubMenues");
        });

        modelBuilder.Entity<TiposCambio>(entity =>
        {
            entity.HasKey(e => e.IdTipoCambio);

            entity.Property(e => e.IdTipoCambio).HasColumnName("idTipoCambio");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("descripcion");
        });

        modelBuilder.Entity<TurnosModificacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TurnosMo__3214EC076D8F0FA5");

            entity.ToTable("TurnosModificacion");

            entity.HasIndex(e => e.EntornoId, "IX_TurnosModificacion_EntornoId");

            entity.HasIndex(e => new { e.EntornoId, e.Estado, e.FechaSolicitud }, "IX_TurnosModificacion_Entorno_Estado_Fecha");

            entity.HasIndex(e => e.Estado, "IX_TurnosModificacion_Estado");

            entity.HasIndex(e => e.FechaSolicitud, "IX_TurnosModificacion_FechaSolicitud");

            entity.HasIndex(e => e.UsuarioId, "IX_TurnosModificacion_UsuarioId");

            entity.HasIndex(e => new { e.UsuarioId, e.Estado }, "IX_TurnosModificacion_Usuario_Activos").HasFilter("([Estado] IN ((1), (2)))");

            entity.Property(e => e.Estado).HasDefaultValue(1, "DF__TurnosMod__Estad__0E391C95");
            entity.Property(e => e.FechaSolicitada).HasColumnType("datetime");
            entity.Property(e => e.FechaSolicitud).HasDefaultValueSql("(getdate())", "DF__TurnosMod__Fecha__0D44F85C");
            entity.Property(e => e.Motivo).HasMaxLength(500);
            entity.Property(e => e.NombreUsuario).HasMaxLength(100);

            entity.HasOne(d => d.Entorno).WithMany(p => p.TurnosModificacions)
                .HasForeignKey(d => d.EntornoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TurnosModificacion_Entorno");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
