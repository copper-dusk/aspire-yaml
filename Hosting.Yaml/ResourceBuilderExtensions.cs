using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CopperDusk.Aspire.Hosting.Yaml;

public static class ResourceBuilderExtensions
{
    internal static IResourceBuilder<YamlFiles> EnsureYaml(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<YamlProvisioner>();

        var existing = builder.Resources.OfType<YamlFiles>().SingleOrDefault();
        if (existing is not null) return builder.CreateResourceBuilder(existing);

        var yaml = new YamlFiles();

        var yamlBuilder = builder
            .AddResource(yaml)
            .WithIconName("DocumentBulletListMultiple")
            .ExcludeFromManifest()
        ;

        yamlBuilder.OnInitializeResource(static async (resource, initializeResourceEvent, cancellationToken) =>
        {
            var eventing = initializeResourceEvent.Eventing;
            var notifications = initializeResourceEvent.Notifications;
            var services = initializeResourceEvent.Services;

            await eventing.PublishAsync(new BeforeResourceStartedEvent(resource, services), cancellationToken);

            await notifications.PublishUpdateAsync(resource, previous => previous with
            {
                State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
            });
        });

        return yamlBuilder;
    }

    public static IResourceBuilder<ContainerResource> WithYamlBindMount(
        this IResourceBuilder<ContainerResource> containerResource,
        IResourceBuilder<YamlSourceResource> yamlResource,
        string target,
        bool isReadOnly = false
    )
    {
        var path = yamlResource.Resource.OutputPath;

        containerResource.WaitForCompletion(yamlResource);

        return containerResource.WithBindMount(
            path,
            target,
            isReadOnly
        );
    }
}
