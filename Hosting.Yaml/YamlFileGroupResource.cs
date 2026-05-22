using static CopperDusk.Aspire.Hosting.Yaml.TempYamlPath;

namespace CopperDusk.Aspire.Hosting.Yaml;

/// <summary>
///     A directory of rendered YAML files. Each member <see cref="YamlSourceResource"/> is
///     rendered to its own temp file by the normal pipeline, then copied into this group's
///     directory under its declared <see cref="YamlSourceResource.FileName"/> so consumers
///     can mount or reference a single folder containing the whole set.
/// </summary>
public class YamlFileGroupResource : IResource, IResourceWithWaitSupport, IValueProvider
{
    public required string Name { get; init; }

    public ResourceAnnotationCollection Annotations { get; } = [];

    internal List<YamlSourceResource> Files { get; } = [];

    private readonly string path = BuildTempYamlPath(Guid.NewGuid().ToString("N"));
    public string Path => path;

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        new(path);
}
