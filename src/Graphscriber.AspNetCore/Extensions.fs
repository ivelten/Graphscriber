namespace Graphscriber.AspNetCore

open System
open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets
open Microsoft.Extensions.DependencyInjection

[<AutoOpen>]
module Extensions =
    let private socketManagerOrDefault (socketManager : IGQLServerSocketManager<'Root> option) (serviceProvider : IServiceProvider) = 
        defaultArg socketManager (serviceProvider.GetService<IGQLServerSocketManager<'Root>>())

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
                    socketManagerOrDefault socketManager this.ApplicationServices,
                    socketFactoryOrDefault socketFactory)

        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            root : 'Root,
                                            ?socketManager : IGQLServerSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLServerSocket) =
            this.UseGQLWebSockets(
                    executor,
                    (fun _ -> root), 
                    socketManagerOrDefault socketManager this.ApplicationServices,
                    socketFactoryOrDefault socketFactory)

    type IServiceCollection with
        member this.AddGQLWebSockets<'Root>() =
            this.AddSingleton<IGQLServerSocketManager<'Root>>(GQLServerSocketManager<'Root>())