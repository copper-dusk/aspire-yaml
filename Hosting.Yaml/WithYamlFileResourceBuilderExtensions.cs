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
    ///     Registers a yaml file with the group: records it for the copy step, re-parents it
    ///     under the group in the dashboard (replacing the default catch-all parent), and
    ///     wires a wait so the group only fires after the file has been rendered.
    /// </summary>
    internal static IResourceBuilder<YamlFileGroupResource> AttachYamlFile(
        this IResourceBuilder<YamlFileGroupResource> group,
        IResourceBuilder<YamlSourceResource> file
    )
    {
        group.Resource.Files.Add(file.Resource);

        // Relationship type string used by Aspire's WithParentRelationship; we strip any
        // existing parent so re-parenting to the group is unambiguous in the dashboard.
        var existingParents = file.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(a => a.Type == "Parent")
            .ToList();

        foreach (var annotation in existingParents)
        {
            file.Resource.Annotations.Remove(annotation);
        }

        file.WithParentRelationship(group);

        group.WaitForCompletion(file);

        return group;
    }
}
