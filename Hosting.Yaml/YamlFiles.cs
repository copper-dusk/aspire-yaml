namespace CopperDusk.Aspire.Hosting.Yaml;

internal class YamlFiles : IResource
{
    public string Name => "yaml";

    public ResourceAnnotationCollection Annotations { get; } = [];
}