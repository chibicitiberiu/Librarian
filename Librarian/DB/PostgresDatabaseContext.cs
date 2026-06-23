using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Librarian.DB
{
    public class PostgresDatabaseContext : DatabaseContext
    {
        public IConfiguration Configuration { get; }

        public PostgresDatabaseContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(Configuration.GetConnectionString("DB"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Postgres-specific GIN indexes backing the full-text search. These live here
            // (not in the provider-agnostic base context) because HasMethod is Npgsql-only.
            // The schema is provisioned through this context's migrations, so declaring them
            // here is enough for them to be created.
            modelBuilder.Entity<IndexedFileContents>()
                .HasIndex(c => c.ContentSearch)
                .HasMethod("gin");
            modelBuilder.Entity<TextAttribute>()
                .HasIndex(a => a.ValueSearch)
                .HasMethod("gin");
        }
    }

    /// <summary>
    /// Used by "dotnet ef" at design time. Reads the connection string from
    /// appsettings.json and environment variables so migrations can target any
    /// database (e.g. set ConnectionStrings__DB to override).
    /// </summary>
    public class PostgresDatabaseContextFactory : IDesignTimeDbContextFactory<PostgresDatabaseContext>
    {
        public PostgresDatabaseContext CreateDbContext(string[] args)
        {
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new PostgresDatabaseContext(config);
        }
    }
}
