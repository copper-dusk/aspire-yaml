using CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;
using static CopperDusk.Aspire.Hosting.Yaml.TempYamlPath;

namespace CopperDusk.Aspire.Hosting.Yaml;

/// <summary>
///     Contains information about how a YAML file will participate in the orchestration.
///     The same source is rendered twice — once for the host perspective and once for the
///     container perspective — so consumers on either side of the host/container boundary can
///     point at a file whose expressions already resolve correctly for their world.
/// </summary>
public class YamlSourceResource : IResource, IValueProvider, PerspectiveAware
{
    public required string Name { get; init; }

    public ResourceAnnotationCollection Annotations { get; init; } = [];

    public required YamlSource Source { get; init; }

    public required string FileName { get; init; }

    private readonly string hostOutputPath = BuildTempYamlPath($"{Guid.NewGuid():N}.host.yaml");
    private readonly string containerOutputPath = BuildTempYamlPath($"{Guid.NewGuid():N}.container.yaml");

    /// <summary>Path of the rendering meant for processes running directly on the developer's machine.</summary>
    public string HostOutputPath => hostOutputPath;

    /// <summary>Path of the rendering meant to be bind-mounted into a container.</summary>
    public string ContainerOutputPath => containerOutputPath;

    /// <summary>
    ///     Default <see cref="IValueProvider"/> view returns the host-perspective path; that's
    ///     the right answer for references coming from outside this library's renderer, where
    ///     "use localhost" is the safe default. When the value is consumed by our own renderer
    ///     it goes through <see cref="PerspectiveAware.GetValueAsync"/> instead and picks the
    ///     path that matches the perspective in play.
    /// </summary>
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        new(hostOutputPath);

    public ValueTask<string?> GetValueAsync(YamlPerspective perspective, CancellationToken cancellationToken) =>
        new(perspective == YamlPerspective.Container ? containerOutputPath : hostOutputPath);
}
