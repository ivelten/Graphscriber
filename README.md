# Graphscriber

Graphscriber is a middleware made for FSharp.Data.GraphQL that adds support for GraphQL over Web Socket specification in popular servers, such as ASP.NET Core applications.

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