namespace CopperDusk.Aspire.Hosting.Yaml;

public static class WithYamlFileResourceBuilderExtensions
{
    public static IResourceBuilder<YamlFileGroupResource> WithYamlFile(
        this IResourceBuilder<YamlFileGroupResource> group,
        string name,
        string content,
        string? fileName = null
    )
    {
        var file = group.ApplicationBuilder.AddYamlFile(name, content, fileName);
        return group.AttachYamlFile(file);
    }

    public static IResourceBuilder<YamlFileGroupResource> WithYamlFile(
        this IResourceBuilder<YamlFileGroupResource> group,
        string name,
        object content,
        string? fileName = null
    )
    {
        var file = group.ApplicationBuilder.AddYamlFile(name, content, fileName);
        
        return group.AttachYamlFile(file);
    }

    public static IResourceBuilder<YamlFileGroupResource> WithYamlFile(
        this IResourceBuilder<YamlFileGroupResource> group,
        string name,
        IEnumerable<YamlSource> documents,
        string? fileName = null
    )
    {
        var file = group.ApplicationBuilder.AddYamlFile(name, documents, fileName);
        
        return group.AttachYamlFile(file);
    }

    /// <summary>
    ///     Registers a yaml file with the group: records it for the copy step and wires a wait
    ///     so the group only fires after the file has been rendered. The file keeps its default
    ///     parent (the root <see cref="YamlFiles"/> resource) since groups are just a flat copy
    ///     step and don't form a real hierarchy in the dashboard.
    /// </summary>
    internal static IResourceBuilder<YamlFileGroupResource> AttachYamlFile(
        this IResourceBuilder<YamlFileGroupResource> group,
        IResourceBuilder<YamlSourceResource> file
    )
    {
        group.Resource.Files.Add(file.Resource);

        group.WaitForCompletion(file);

        return group;
    }
}
