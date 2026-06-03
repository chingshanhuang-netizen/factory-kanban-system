namespace TPS.Nexus.Core;

/// <summary>
/// Stub for TPS.Nexus.Core.dll — provides WebRootPath without direct IWebHostEnvironment dependency.
/// Register in host: services.AddSingleton&lt;IWebHostEnvironmentAccessor&gt;(new WebHostEnvironmentAccessor(app.Environment.WebRootPath));
/// </summary>
public interface IWebHostEnvironmentAccessor
{
    string WebRootPath { get; }
}
