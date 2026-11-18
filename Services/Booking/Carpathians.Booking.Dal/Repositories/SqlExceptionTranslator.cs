using Carpathians.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public static class SqlExceptionTranslator
{
    public static bool TryTranslate(SqlException ex, out Exception domain)
    {
        domain = ex.Number switch
        {
            50001                  => new NotFoundException(ex.Message),
            50002 or 50003 or 50007 => new BusinessConflictException(ex.Message),
            50005 or 50006         => new ValidationException(ex.Message),
            2601 or 2627           => new BusinessConflictException("Record with the same key already exists"),
            547                    => new BusinessConflictException("Operation violates a data constraint"),
            _                      => null!
        };
        return domain is not null;
    }
}