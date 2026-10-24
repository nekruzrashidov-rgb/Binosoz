using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Sitora.Infrastructure.Data;

public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
