# Graphscriber

Graphscriber is a middleware made for FSharp.Data.GraphQL that adds support for GraphQL over Web Socket specification in popular servers, such as ASP.NET Core applications.

## Quickstart

To integrate everything, you just need to add a reference to `Graphscriber.AspNetCore` namespace, and call the extension method `UseGQLWebSockets`:

```fsharp
type Startup private () =
    // ...
    member __.Configure(app: IApplicationBuilder) =
        let executor = MySchema.executor // Any other instance of Executor<'Root>
        let root = MySchema.root         // Any any other instance of root object of type 'Root
        app.UseGQLWebSockets(executor, root)
```