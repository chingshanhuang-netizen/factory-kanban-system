using System.Data.Common;

namespace TPS.Nexus.Core;

/// <summary>
/// Stub for TPS.Nexus.Core.dll — replace with actual DLL in production deployment.
/// Provides database connection abstraction (MySQL in production).
/// Returns DbConnection so callers can use OpenAsync / ExecuteReaderAsync etc.
/// </summary>
public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
