namespace TPS.Nexus.Core;

/// <summary>
/// Stub for TPS.Nexus.Core.dll — replace with actual DLL in production deployment.
/// Checks whether the current user has a named function permission.
/// </summary>
public interface IFunctionPermissionService
{
    Task<bool> HasPermissionAsync(string permissionCode);
}
