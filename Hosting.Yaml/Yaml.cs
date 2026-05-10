namespace CopperDusk.Aspire.Hosting.Yaml;

internal class Yaml : IResource
{
    public string Name => "yaml";

    public ResourceAnnotationCollection Annotations { get; } = [];
    
    private readonly IDictionary<string, YamlSourceResource> yamlResources = new Dictionary<string, YamlSourceResource>();

    public void AddYamlResource(YamlSourceResource yamlSourceResource)
    {
        yamlResources.Add(yamlSourceResource.OutputPath, yamlSourceResource);
    }
}