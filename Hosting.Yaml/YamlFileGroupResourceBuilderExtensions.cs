using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CopperDusk.Aspire.Hosting.Yaml;

public static class YamlFileGroupResourceBuilderExtensions
{
    public static IResourceBuilder<YamlFileGroupResource> AddYamlFileGroup(
        this IDistributedApplicationBuilder builder,
        string name
    ) => builder.AddYamlFileGroup(name, []);

    public static IResourceBuilder<YamlFileGroupResource> AddYamlFileGroup(
        this IDistributedApplicationBuilder builder,
        string name,
        IEnumerable<IResourceBuilder<YamlSourceResource>> files
    )
    {
        var currentYaml = builder.EnsureYaml();

        var resource = new YamlFileGroupResource { Name = name };

        var groupBuilder = builder
            .AddResource(resource)
            .WithIconName("FolderClosed")
            .WithParentRelationship(currentYaml)
            .ExcludeFromManifest()
        ;

        groupBuilder.OnInitializeResource(OnFileGroupInitialize);

        foreach (var file in files)
        {
            groupBuilder.AttachYamlFile(file);
        }

        return groupBuilder;
    }

    private static async Task OnFileGroupInitialize(YamlFileGroupResource group, InitializeResourceEvent initializeResourceEvent, CancellationToken cancellationToken)
    {
        var eventing = initializeResourceEvent.Eventing;
        var notifications = initializeResourceEvent.Notifications;
        var services = initializeResourceEvent.Services;

        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(group);

        try
        {
            Directory.CreateDirectory(group.HostPath);
            Directory.CreateDirectory(group.ContainerPath);

            await eventing.PublishAsync(new BeforeResourceStartedEvent(group, services), cancellationToken);

            foreach (var file in group.Files)
            {
                var fileName = file.FileName ?? $"{file.Name}.yaml";

                var hostDestination = Path.Combine(group.HostPath, fileName);
                File.Copy(file.HostOutputPath, hostDestination, overwrite: true);
                logger.LogInformation("Copied {Source} to {Destination}", file.HostOutputPath, hostDestination);

                var containerDestination = Path.Combine(group.ContainerPath, fileName);
                File.Copy(file.ContainerOutputPath, containerDestination, overwrite: true);
                logger.LogInformation("Copied {Source} to {Destination}", file.ContainerOutputPath, containerDestination);
            }

            await notifications.PublishUpdateAsync(group, previous => previous with { State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success), });

            await eventing.PublishAsync(new ResourceReadyEvent(group, services), cancellationToken);

            await notifications.PublishUpdateAsync(group, previous => previous with { State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success), });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision YAML file group {ResourceName}.", group.Name);

            await notifications.PublishUpdateAsync(group, previous => previous with { State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error), });
        }
    }
}
