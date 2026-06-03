using System.Data;

namespace TPS.Nexus.Core;

/// <summary>
/// Stub for TPS.Nexus.Core.dll — replace with actual DLL in production deployment.
/// Provides database connection abstraction (MySQL in production).
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
