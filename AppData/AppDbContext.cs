// dotnet tool install --global dotnet-ef
// dotnet tool update --global dotnet-ef
// dotnet add package Microsoft.EntityFrameworkCore.Sqlite
// dotnet add package Microsoft.EntityFrameworkCore.Design
// dotnet add package Microsoft.EntityFrameworkCore.Tools
// dotnet ef migrations add Migration1 
// dotnet ef database update 

using System;
using Microsoft.EntityFrameworkCore;
using Vokabeln.AppData.Entities;

namespace Vokabeln.AppData;

public class AppDbContext : DbContext
{
    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


    public DbSet<WordEntity> Words => Set<WordEntity>();
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameAnswerEntity> GameAnswers => Set<GameAnswerEntity>();

    // Fiil çekimleri: her zaman ayrı tabloda
    public DbSet<PraesensEntity> Praesens => Set<PraesensEntity>();
    public DbSet<PraeteritumEntity> Praeteritum => Set<PraeteritumEntity>();
    public DbSet<PerfektEntity> Perfekt => Set<PerfektEntity>();
    public DbSet<FuturIEntity> FuturI => Set<FuturIEntity>();
    public DbSet<KonjunktivIIEntity> KonjunktivII => Set<KonjunktivIIEntity>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampWords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampWords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Kelimelerin oluşturma/güncelleme tarihleri (UTC). Import'ta dosyadaki tarihler varsa korunur.
    private void StampWords()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<WordEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                if (entry.Entity.UpdatedAt == default) entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
#endif

        if (!optionsBuilder.IsConfigured)
        {
            // TODO To enable debugging in Migrations, uncomment following:
            // if (System.Diagnostics.Debugger.IsAttached == false) System.Diagnostics.Debugger.Launch();

            optionsBuilder.UseSqlite($"Data Source=Vokabeln.db");

        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO To enable debugging in Migrations, uncomment following:
        // if (System.Diagnostics.Debugger.IsAttached == false) System.Diagnostics.Debugger.Launch(); 

        modelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());


    }
}
