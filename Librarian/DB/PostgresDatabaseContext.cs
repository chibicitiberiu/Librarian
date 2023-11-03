using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

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

    public class PostgresDatabaseContextFactory : IDesignTimeDbContextFactory<PostgresDatabaseContext>
    {
        public PostgresDatabaseContext CreateDbContext(string[] args)
        {
            var dict = new Dictionary<string, string>()
            {
                { "ConnectionStrings:DB", "Database=postgres;Host=10.0.0.10;Port=5555;Username=postgres;Password=secretpassword123" }
            };

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(dict)
                .Build();

            return new PostgresDatabaseContext(config);
        }
    }
}
