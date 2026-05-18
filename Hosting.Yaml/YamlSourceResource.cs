namespace CopperDusk.Aspire.Hosting.Yaml;

public class YamlSourceResource : IResource, IValueProvider
{
    public required string Name { get; init; }

    public ResourceAnnotationCollection Annotations { get; init; } = [];

    public required YamlSource Source { get; init; }

    public string? FileName { get; init; }

    private readonly string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
    public string OutputPath => outputPath;

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        new(outputPath);
}
