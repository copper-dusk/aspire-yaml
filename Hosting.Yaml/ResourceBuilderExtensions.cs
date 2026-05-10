using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CopperDusk.Aspire.Hosting.Yaml;

public static class ResourceBuilderExtensions
{
    private static Yaml? yaml;
    
    private static IResourceBuilder<Yaml> EnsureYaml(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<YamlProvisioner>();
        
        yaml ??= new();

        var yamlResource = builder
            .AddResource(yaml)
            .WithIconName("documentation")
            .ExcludeFromManifest()
        ;

        yamlResource.OnInitializeResource(static async (_, initializeResourceEvent, cancellationToken) =>
        {
            var eventing = initializeResourceEvent.Eventing;
            var notifications = initializeResourceEvent.Notifications;
            var services = initializeResourceEvent.Services;

            // note: This is necessary so that anything that has an outstanding .WaitFor will be notified.
            await eventing.PublishAsync(new BeforeResourceStartedEvent(yaml, services), cancellationToken);
            
            var yamlProvisioner = initializeResourceEvent.Services.GetRequiredService<YamlProvisioner>();
            
            await yamlProvisioner.ProvisionYamlAsync(yaml, cancellationToken);
            
            await notifications.PublishUpdateAsync(yaml, (previous) => previous with
            {
                State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
            });
        });
        
        return yamlResource;
    }
    
    public static IResourceBuilder<YamlSourceResource> AddYaml(
        this IDistributedApplicationBuilder builder,
        string name,
        string content,
        string? fileName = null
    )
    {
        var yaml = builder.EnsureYaml();
        
        // todo: Detect yaml, json or path content

        return builder
            .AddResource(new YamlSourceResource
            {
                Name = name,
                Source = new RawJsonSource(content),
                FileName = fileName ?? $"{name}.yaml",
            })
            .WithParentRelationship(yaml)
        ;
    }

    public static IResourceBuilder<ContainerResource> WithYamlBindMount(
        this IResourceBuilder<ContainerResource> containerResource,
        IResourceBuilder<YamlSourceResource> yamlResource,
        string target,
        bool isReadOnly = false
    )
    {
        var path = yamlResource.Resource.OutputPath;
        
        containerResource.WaitFor(yamlResource);
        
        return containerResource.WithBindMount(
            path,
            target,
            isReadOnly
        );
    }
}