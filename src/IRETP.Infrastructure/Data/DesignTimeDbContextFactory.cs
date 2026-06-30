using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IRETP.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IretpDbContext>
{
    public IretpDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../IRETP.WebAPI"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<IretpDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"),
            b => b.MigrationsAssembly(typeof(IretpDbContext).Assembly.FullName));

        return new IretpDbContext(optionsBuilder.Options);
    }
}
