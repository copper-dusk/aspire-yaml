namespace CopperDusk.Aspire.Hosting.Yaml;

internal class Yaml : IResource
{
    public string Name => "yaml";

    public ResourceAnnotationCollection Annotations { get; } = [];
    
    private readonly List<YamlSourceResource> yamlResources = [];
    public IEnumerable<YamlSourceResource> YamlResources => yamlResources;

    public void AddYamlResource(YamlSourceResource yamlSourceResource)
    {
        yamlResources.Add(yamlSourceResource);
    }
}