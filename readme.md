# CopperDusk.Aspire.Hosting.Yaml

An Aspire hosting integration for authoring YAML files inline in your AppHost and
materializing them to disk at startup, ready to be bind-mounted into containers.

I despise working with YAML, but even I can't deny that they have their uses and 
are ubiquitous.  From time to time, you may need to include YAML files in your 
deployments.  Naturally, managing files in a project, bundling them on 
output, and then referencing them at runtime is convoluted and unwieldy.

So, I made this library to let you manage YAML files at runtime!

The intent is to let you declare configuration files (Dapr components, Kubernetes
manifests, app config, etc.) alongside the rest of your distributed application
model — so they can reference Aspire parameters, resources, and endpoints
directly instead of being maintained as separate static files.

## How it works

`AddYaml` registers a `YamlSourceResource`. When the AppHost starts, a shared
`YamlProvisioner` renders each source to a temp-file path exposed as
`OutputPath`, and `WithYamlBindMount` wires that path into a container as a bind
mount, waiting for the YAML resource to finish rendering first.

Object sources are walked recursively: `IResourceBuilder<T>` is unwrapped,
`IValueProvider` values (parameters, references, endpoint properties) are
awaited, dictionaries and enumerables are flattened, and everything else is
reflected over as a structured object. This is what lets you embed
`ReferenceExpression.Create($"... {postgres.GetEndpoint("tcp").Property(...)} ...")`
inside the object passed to `AddYaml` and have it resolve at render time.

## Why not incremental source generators?

It's a great thought, honestly!  The main driving factor is that these YAML files 
are more than just static output.

Specifically, we support flowing values and expressions from Aspire into the YAML files.
This cannot be achieved with incremental source generators, as the Aspire orchestration 
is only available at runtime and also changes between runs (think ports)!

Second, depending on how your project is defined and the Aspire target, the YAML files 
may be handled in very different ways.

So yeah, hopefully this explanation is sufficient and also helps you understand the reasoning
behind this library.

## API

All extensions live on `IDistributedApplicationBuilder`:

- `AddYaml(name, string content, fileName?)` — raw YAML or raw JSON (auto-detected
  by leading `{` / `[`). Raw YAML is parsed once to validate it.
- `AddYaml(name, object content, fileName?)` — an anonymous (or any CLR) object,
  serialized via YamlDotNet after value resolution.
- `AddYaml(name, IEnumerable<YamlSource> documents, fileName?)` — pre-built
  sources bundled into a single multi-document stream (`---`-separated).
- `AddYamlDocuments(name, IEnumerable<object> documents, fileName?)` — convenience
  for multi-document output from plain objects.
- `WithYamlBindMount(yamlResource, target, isReadOnly?)` — mounts a rendered YAML
  file into a container at `target` and waits for it to finish rendering.

`YamlSource` variants: `ObjectYamlSource` (public), plus internal
`RawYamlSource`, `RawJsonSource`, and `MultiDocumentYamlSource`.

## Example

See `AppHost/AppHost.cs` for the full sample. The interesting piece:

```csharp
var postgres = builder.AddPostgres("postgres", username, password);
var stateDatabase = postgres.AddDatabase("state");

var component = builder.AddYaml("test-object", new
{
    apiVersion = "dapr.io/v1alpha1",
    kind = "Component",
    metadata = new { name = "test-state" },
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
                    $"host={postgres.Resource.Name} user={username.Resource} password={password.Resource} port={postgres.GetEndpoint("tcp").Property(EndpointProperty.TargetPort)} connect_timeout=10 database={stateDatabase.Resource.DatabaseName}"
                ),
            },
        },
    },
});

builder.AddDiagridDashboard(/* ... */)
    .WithYamlBindMount(component, "/app/components/test-json.yaml")
    .WaitFor(stateDatabase);
```

## Project layout

- `Hosting.Yaml/` — the library (`net10.0`, targets `Aspire.Hosting.AppHost`
  13.3.4 and `YamlDotNet` 17.1.0).
- `AppHost/` — a sample AppHost that exercises the API against Postgres and the
  Diagrid Dapr dashboard.

## About

This project is created and maintained by [Alexander Trauzzi](https://github.com/atrauzzi).
