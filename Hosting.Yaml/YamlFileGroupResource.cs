using CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint;
using static CopperDusk.Aspire.Hosting.Yaml.TempYamlPath;

namespace CopperDusk.Aspire.Hosting.Yaml;

/// <summary>
///     A directory of rendered YAML files. Each member <see cref="YamlSourceResource"/> is
///     rendered to its own pair of temp files by the normal pipeline, then copied into this
///     group's <see cref="HostPath"/> and <see cref="ContainerPath"/> directories so consumers
///     can mount or reference a single folder containing the whole set in either perspective.
/// </summary>
public class YamlFileGroupResource : IResource, IResourceWithWaitSupport, IValueProvider, PerspectiveAware
{
    public required string Name { get; init; }

    public ResourceAnnotationCollection Annotations { get; } = [];

    internal List<YamlSourceResource> Files { get; } = [];

    // Single identifier shared by both directories — only the perspective suffix varies.
    private readonly string identifier = Guid.NewGuid().ToString("N");

    /// <summary>Directory of renderings meant for host-side consumers.</summary>
    public string HostPath => BuildTempYamlPath($"{identifier}.host");

    /// <summary>Directory of renderings meant to be bind-mounted into a container.</summary>
    public string ContainerPath => BuildTempYamlPath($"{identifier}.container");

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        new(HostPath);

    public ValueTask<string?> GetValueAsync(YamlPerspective perspective, CancellationToken cancellationToken) =>
        new(perspective == YamlPerspective.Container ? ContainerPath : HostPath);
}
