# Graphscriber

Graphscriber is a middleware made for FSharp.Data.GraphQL that adds support for GraphQL over Web Socket specification in popular servers, such as ASP.NET Core applications.

| Windows | Linux |
| :------ | :---- |
| [![Windows Build status](https://ci.appveyor.com/api/projects/status/92u6ebmh7hbgvxaq/branch/master?svg=true)](https://ci.appveyor.com/project/ivelten/graphscriber/branch/develop) | [![Linux Build Status](https://travis-ci.org/ivelten/Graphscriber.svg?branch=develop)](https://travis-ci.org/ivelten/Graphscriber?branch=develop) |
| [![Windows Build History](https://buildstats.info/appveyor/chart/ivelten/graphscriber?branch=develop&includeBuildsFromPullRequest=false)](https://ci.appveyor.com/project/ivelten/graphscriber/history?branch=develop) | [![Linux Build History](https://buildstats.info/travisci/chart/ivelten/Graphscriber?branch=develop&includeBuildsFromPullRequest=false)](https://travis-ci.org/ivelten/Graphscriber/builds?branch=develop) |

## Quickstart

To integrate everything, you just need to add a reference to `Graphscriber.AspNetCore` namespace, and call the extension method `UseGQLWebSockets`. You will also need an instance of `IGQLServerSocketManager`, an object that handles all subscriptions and sockets. A default, in-memory, singleton implementation can be added to the service collection of the application by calling `AddGQLServerSocketManager` extension method of `IServiceCollection` inside `ConfigureServices` of the `Startup` class. You can also provide your own implemetation and add it to the service collection, or providing it as an optional parameter of `UseGQLWebSockets`.

```fsharp
open Graphscriber.AspNetCore

type Startup private () =
    member __.ConfigureServices(services : IServiceCollection) =
        // This method inserts an in memory, singleton server socket manager
        // If socket management customization is needed, a custom implementation of
        // IGQLServerSocketManager<Root> needs to be created and provided
        services.AddGQLServerSocketManager<Root>() |> ignore

    member __.Configure(app: IApplicationBuilder) =
        let executor = MySchema.executor // An instance of Executor<Root>
        let root = MySchema.root         // An instance of root object of type Root
        app.UseGQLWebSockets(executor, root)
```