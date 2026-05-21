using CommunityToolkit.Aspire.Hosting.Dapr;
using CopperDusk.Aspire.Hosting.Yaml;
using Diagrid.Aspire.Hosting.Dashboard;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

var username = builder.AddParameter("username", "local");
var password = builder.AddParameter("password", "zxczxc123", true);

var postgres = builder.AddPostgres("postgres", username, password);
var stateDatabase = postgres.AddDatabase("state");

var testJsonYaml = builder.AddYamlFile("test-json", """
{ "hi": "test" }
""");

var testMultiDocYaml = builder.AddYamlFile("test-multi-doc", [
    new ObjectYamlSource(new
    {
        test = "hi",
    }),
    new ObjectYamlSource(new
    {
        otherTest = "hi too!",
    }),
]);

var diagridDashboardState = builder.AddYamlFile("diagrid-dashboard-state", new
{
    apiVersion = "dapr.io/v1alpha1",
    kind = "Component",
    metadata = new
    {
        name = "test-state",
    },
    spec = new
    {
        type = "state.postgresql",
        version = "v2",
        metadata = new object[]
        {
            new
            {
                name = "connectionString",
                value = ReferenceExpression.Create(
                    // note: If you ever want the host-side view (Dapr running outside containers), swap postgres.Resource.Name → ...Property(EndpointProperty.Host) and TargetPort → Port.
                    $"host={postgres.Resource.Name} user={username.Resource} password={password.Resource} port={postgres.GetEndpoint("tcp").Property(EndpointProperty.TargetPort)} connect_timeout=10 database={stateDatabase.Resource.DatabaseName}"
                ),
            },
        },
    },
});

// port = dashboard.GetEndpoint("http").Property(EndpointProperty.Port),
var dashboard = builder
    .AddDiagridDashboard(configuration: new()
    {
        ComponentFile = diagridDashboardState.Resource.FileName,
    })
    .WithReference(postgres)
    .WithYamlBindMount(diagridDashboardState, $"/app/components/{diagridDashboardState.Resource.FileName}")
    .WaitFor(stateDatabase);

var daprComponents = builder.AddYamlFileGroup("dapr-components", 
[
    builder.AddYamlFile("dapr-state", new
    {
        apiVersion = "dapr.io/v1alpha1",
        kind = "Component",
        metadata = new
        {
            name = "state",
        },
        spec = new
        {
            type = "state.postgresql",
            version = "v2",
            metadata = new object[]
            {
                new
                {
                    name = "connectionString",
                    value = ReferenceExpression.Create(
                        $"host={postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host)} user={username.Resource} password={password.Resource} port={postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port)} connect_timeout=10 database={stateDatabase.Resource.DatabaseName}"
                    ),
                },
            },
        }, 
    }),
]);

var testApi = builder
    .AddProject<TestApi>("test-api")
    .WaitFor(stateDatabase)
    .WithDaprSidecar(new DaprSidecarOptions
    {
        ResourcesPaths = [
            daprComponents.Resource.Path,
        ],
    });

builder.Build().Run();