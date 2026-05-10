namespace CopperDusk.Aspire.Hosting.Yaml;

public class YamlSourceResource : IResource
{
    public required string Name { get; init; }

    public ResourceAnnotationCollection Annotations { get; init; } = [];
    
    public required YamlSource Source { get; init; }
    
    public string? FileName { get; init; }

    public string OutputPath => ""; // todo
}
