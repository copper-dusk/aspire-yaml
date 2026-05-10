using CopperDusk.Aspire.Hosting.Yaml;
using Diagrid.Aspire.Hosting.Dashboard;

var builder = DistributedApplication.CreateBuilder(args);

var testJsonYaml = builder.AddYaml("test-json", """
{ "hi": "test" }
""");

builder
    .AddDiagridDashboard()
    .WithYamlBindMount(testJsonYaml, "/app/test-json.yaml");

builder.Build().Run();