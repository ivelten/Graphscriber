namespace Graphscriber.AspNetCore

open System
open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets
open Microsoft.Extensions.DependencyInjection

[<AutoOpen>]
module Extensions =
    let private socketManagerOrDefault (socketManager : IGQLServerSocketManager<'Root> option) (serviceProvider : IServiceProvider) =
        match socketManager with
        | Some mgr -> mgr
        | None ->
            match serviceProvider.GetService<IGQLServerSocketManager<'Root>>() with
            | null -> raise <| InvalidOperationException("No server socket manager implementation is registered. You must add a implementation of IGQLServerSocketManager<'Root> to the service collection of the application, or provide one in the middleware arguments.")
            | mgr -> mgr

    let private socketFactoryOrDefault (socketFactory : (WebSocket -> IGQLServerSocket) option) =
        defaultArg socketFactory (fun socket -> upcast new GQLServerSocket(socket))

    type IApplicationBuilder with
        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            rootFactory : IGQLServerSocket -> 'Root, 
                                            ?socketManager : IGQLServerSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLServerSocket) =
            this.UseWebSockets()
                .UseMiddleware<GQLWebSocketMiddleware<'Root>>(
                    executor,
                    rootFactory, 
                    socketManagerOrDefault socketManager (this.ApplicationServices),
                    socketFactoryOrDefault socketFactory)

        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            root : 'Root,
                                            ?socketManager : IGQLServerSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLServerSocket) =
            this.UseGQLWebSockets(
                    executor,
                    (fun _ -> root), 
                    socketManagerOrDefault socketManager (this.ApplicationServices),
                    socketFactoryOrDefault socketFactory)

    type IServiceCollection with
        member this.AddGQLServerSocketManager<'Root>(?connectionHandler : Map<string, obj> option -> GQLConnectionResult) =
            let connectionHandler = defaultArg connectionHandler (fun _ -> Accept)
            this.AddSingleton<IGQLServerSocketManager<'Root>>(GQLServerSocketManager<'Root>(connectionHandler))