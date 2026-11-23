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
