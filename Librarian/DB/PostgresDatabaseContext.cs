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
