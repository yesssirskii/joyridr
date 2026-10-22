using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Joyridr.Common;

public static class PostgresErrors
{
    /// <summary>True when the save failed because of the ride overlap exclusion constraint.</summary>
    public static bool IsExclusionViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };

    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
