# CopperDusk.Aspire.Hosting.Yaml

![NuGet Version](https://img.shields.io/nuget/v/CopperDusk.Aspire.Hosting.Yaml)

An Aspire hosting integration for dynamically authoring YAML files from your AppHost and 
materializing them to disk at startup, ready for use!

I despise working with YAML, but even I can't deny that it has its uses and 
is ubiquitous.  As a result, from time to time, you may need to include YAML files 
in your deployments.  Naturally, managing files in a project, bundling them on 
output, and then referencing them at runtime is convoluted and unwieldy.

So, I made this library to let you manage YAML files at runtime from Aspire!

The idea is to let you declare configuration files (Dapr components, Kubernetes
manifests, app config, etc.) alongside the rest of your distributed application
model.  Then, they can reference Aspire parameters, resources, and endpoints
directly instead of being maintained as separate static files.  Want strong typing 
for YAML data structures?  [No probalo!](https://www.youtube.com/watch?v=ToJlOND8TfU)

## How it works

`AddYamlFile` registers a `YamlSourceResource`. When the AppHost starts, a shared
`YamlProvisioner` renders each source to disk twice — once for the host
perspective and once for the container perspective — exposing the resulting
files as `HostOutputPath` and `ContainerOutputPath`. `WithYamlBindMount` wires
the container rendering into a container as a bind mount, waiting for the
YAML resource to finish rendering first. See
[Perspectives and bifurcation](#perspectives-and-bifurcation) for what the two
renderings differ on and how to author values that participate.

For consumers that expect a directory of files rather than individual paths
(Dapr's `ResourcesPaths`, Kubernetes manifest folders, etc.), `AddYamlFileGroup`
registers a `YamlFileGroupResource`. Each member file is rendered through the
normal pipeline, then copied into the group's `HostPath` and `ContainerPath`
directories under its declared `FileName`.

Object sources are walked recursively: `IResourceBuilder<T>` is unwrapped,
`IValueProvider` values (parameters, references, endpoint properties) are
awaited, dictionaries and enumerables are flattened, and everything else is
reflected over as a structured object. This is what lets you embed
`ReferenceExpression.Create($"... {postgres.GetEndpointForYaml("tcp").Host} ...")`
inside the object passed to `AddYamlFile` and have it resolve at render time.

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

## Perspectives and bifurcation

Aspire runs services from two different vantage points inside the same
orchestration:

- **Host view** — a process the developer runs directly: a Dapr sidecar started
  by `dapr run`, an executable resource, an integration test runner. Endpoints
  reach other services through Aspire's proxy at `localhost:<proxied-port>`.
- **Container view** — a process running inside a container that Aspire puts on
  the same docker network. Endpoints reach other services by the resource's
  name (which Aspire wires up as DNS) and the in-container target port.

A YAML file's expressions resolve correctly only for one of these views. Hard
code `EndpointProperty.Host`/`Port` and the file is broken for containers; hard
code the resource name and target port, and it's broken on the host.

This library handles that by rendering every `YamlSourceResource` **twice** —
one file for each perspective — and pointing consumers at the right one
automatically:

- `WithYamlBindMount` mounts the *container* rendering into containers.
- `Resource.HostOutputPath` / `Resource.HostPath` give you the *host* rendering,
  for things like `DaprSidecarOptions.ResourcesPaths`.

![Perspective resolution](perspectives.svg)

To make a single template usable in both renderings, wrap endpoint references
with `GetEndpointForYaml`, which mirrors Aspire's `GetEndpoint` but returns a
`BifurcatedEndpoint`:

```csharp
value = ReferenceExpression.Create(
    $"host={postgres.GetEndpointForYaml("tcp").Host} port={postgres.GetEndpointForYaml("tcp").Port} ..."
)
```

`BifurcatedEndpoint.Host`, `.Port`, and `.Address` are `PerspectiveValue`s —
host/container pairs that the renderer pulls apart at the appropriate moment.
The host pass picks the proxied host/port; the container pass swaps in the
resource's DNS name and the target port. Resolution descends through
`ReferenceExpression`, so these can be interpolated freely inside any template
the renderer touches.

For values whose host and container forms diverge in *structure* (not just
host/port — e.g. an entirely different connection-string layout), construct a
`PerspectiveValue` directly with two pre-built expressions:

```csharp
value = new PerspectiveValue(
    host: ReferenceExpression.Create($"... localhost-specific layout ..."),
    container: ReferenceExpression.Create($"... container-specific layout ...")
);
```

The bifurcation vocabulary lives in
`CopperDusk.Aspire.Hosting.Yaml.BifurcatedEndpoint`.

## API

### Single files

Extensions on `IDistributedApplicationBuilder` for producing one rendered file:

- `AddYamlFile(name, string content, fileName?)` — raw YAML or raw JSON
  (auto-detected by leading `{` / `[`). Raw YAML is parsed once to validate it.
- `AddYamlFile(name, object content, fileName?)` — an anonymous (or any CLR)
  object, serialized via YamlDotNet after value resolution.
- `AddYamlFile(name, IEnumerable<YamlSource> documents, fileName?)` — pre-built
  sources bundled into a single multi-document stream (`---`-separated). Use this
  via collection-expression syntax (`[sourceA, sourceB]`) to mix different
  source kinds in one file.
- `AddMultiYamlFile(name, IEnumerable<object> documents, fileName?)` —
  convenience for a multi-document file built from plain objects.
- `WithYamlBindMount(yamlResource, target, isReadOnly?)` — mounts a rendered
  YAML file into a container at `target` and waits for it to finish rendering.

`YamlSource` variants: `ObjectYamlSource` (public), plus internal
`RawYamlSource`, `RawJsonSource`, and `MultiDocumentYamlSource`.

### File groups

For consumers that need a directory of files instead of a single path:

- `AddYamlFileGroup(name)` — creates an empty group. Files can be attached later
  via the `WithYamlFile` / `WithYamlDocuments` extensions below.
- `AddYamlFileGroup(name, IEnumerable<IResourceBuilder<YamlSourceResource>> files)` —
  creates a group seeded with already-declared file resources. Each file is
  rendered independently, then copied into the group's directory.
- `WithYamlFile(group, name, string | object | IEnumerable<YamlSource>, fileName?)` —
  declares a new file directly on a group; equivalent to calling `AddYamlFile`
  and attaching the result.
- `WithYamlDocuments(group, name, IEnumerable<object>, fileName?)` — declares a
  new multi-document file directly on a group.
- `WithYamlBindMount(yamlFileGroup, target, isReadOnly?)` — mounts the group's
  whole rendered directory into a container at `target` and waits for the group
  to finish rendering.

The group exposes `Resource.HostPath` and `Resource.ContainerPath` (rendered
directories, one per perspective). Pick `HostPath` for host-side integrations
such as Dapr's `DaprSidecarOptions.ResourcesPaths` (the sidecar runs as a host
process); `ContainerPath` is what `WithYamlBindMount` mounts into containers.

## Example

See `AppHost/AppHost.cs` for the full sample.

### Bind-mounting a single rendered file

```csharp
var postgres = builder.AddPostgres("postgres", username, password);
var stateDatabase = postgres.AddDatabase("state");

var component = builder.AddYamlFile("test-object", new
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
                    $"host={postgres.GetEndpointForYaml("tcp").Host} user={username.Resource} password={password.Resource} port={postgres.GetEndpointForYaml("tcp").Port} connect_timeout=10 database={stateDatabase.Resource.DatabaseName}"
                ),
            },
        },
    },
});

builder.AddSomeContainer(/* ... */)
    .WithYamlBindMount(component, "/app/components/test-json.yaml")
    .WaitFor(stateDatabase);
```

### Multi-document files

A collection expression of `YamlSource` values renders to one file with each
document separated by `---`:

```csharp
var multi = builder.AddYamlFile("test-multi-doc", [
    new ObjectYamlSource(new { test = "hi" }),
    new ObjectYamlSource(new { otherTest = "hi too!" }),
]);
```

### File groups (directory of files)

`AddYamlFileGroup` produces a directory containing every member file, which is
the shape consumers like Dapr's `ResourcesPaths` expect:

```csharp
var daprComponents = builder.AddYamlFileGroup("dapr-components",
[
    builder.AddYamlFile("dapr-state", new
    {
        apiVersion = "dapr.io/v1alpha1",
        kind = "Component",
        metadata = new { name = "state" },
        spec = new { /* ... */ },
    }),
]);

builder
    .AddProject<TestApi>("test-api")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        ResourcesPaths = [
            // Dapr sidecar runs as a host process; reach for HostPath.
            daprComponents.Resource.HostPath,
        ],
    });
```

Or bind-mount the whole group directory into a container:

```csharp
builder.AddSomeContainer(/* ... */)
    .WithYamlBindMount(daprComponents, "/app/components");
```

## Project layout

- `Hosting.Yaml/` — the library (`net10.0`, targets `Aspire.Hosting.AppHost`
  13.3.4 and `YamlDotNet` 17.1.0).
- `AppHost/` — a sample AppHost that exercises the API.

## About

This project is created and maintained by [Alexander Trauzzi](https://github.com/atrauzzi) in what little spare time he has.

_No, I don't normally refer to myself in the third person..._
