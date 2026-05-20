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

        groupBuilder.OnInitializeResource(static async (group, initializeResourceEvent, cancellationToken) =>
        {
            var eventing = initializeResourceEvent.Eventing;
            var notifications = initializeResourceEvent.Notifications;
            var services = initializeResourceEvent.Services;

            var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(group);

            try
            {
                Directory.CreateDirectory(group.Path);

                await eventing.PublishAsync(new BeforeResourceStartedEvent(group, services), cancellationToken);

                foreach (var file in group.Files)
                {
                    var destination = Path.Combine(group.Path, file.FileName ?? $"{file.Name}.yaml");
                    File.Copy(file.OutputPath, destination, overwrite: true);
                    logger.LogInformation("Copied {Source} to {Destination}", file.OutputPath, destination);
                }

                await notifications.PublishUpdateAsync(group, previous => previous with
                {
                    State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                });

                await eventing.PublishAsync(new ResourceReadyEvent(group, services), cancellationToken);

                await notifications.PublishUpdateAsync(group, previous => previous with
                {
                    State = new(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to provision YAML file group {ResourceName}.", group.Name);

                await notifications.PublishUpdateAsync(group, previous => previous with
                {
                    State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                });
            }
        });

        foreach (var file in files)
        {
            groupBuilder.AttachYamlFile(file);
        }

        return groupBuilder;
    }
}
