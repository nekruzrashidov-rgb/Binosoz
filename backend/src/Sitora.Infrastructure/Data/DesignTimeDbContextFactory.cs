using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sitora.Infrastructure.Data;

// Нужна только для `dotnet ef migrations add`: подключение к базе при этом не открывается.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=sitora_design")
            .Options;
        return new AppDbContext(options, TimeProvider.System);
    }
}
