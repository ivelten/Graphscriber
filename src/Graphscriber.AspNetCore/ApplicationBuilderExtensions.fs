namespace Graphscriber.AspNetCore

open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets

[<AutoOpen>]
module ApplicationBuilderExtensions =
    let private socketManagerOrDefault (socketManager : IGQLServerSocketManager<'Root> option) = 
        defaultArg socketManager (upcast GQLServerSocketManager())

    let private socketFactoryOrDefault (socketFactory : (WebSocket -> IGQLServerSocket) option) =
        defaultArg socketFactory (fun ws -> upcast new GQLServerSocket(ws))

    type IApplicationBuilder with
        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            rootFactory : IGQLServerSocket -> 'Root, 
                                            ?socketManager : IGQLServerSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLServerSocket) =
            this.UseWebSockets()
                .UseMiddleware<GQLWebSocketMiddleware<'Root>>(
                    executor,
                    rootFactory, 
                    socketManagerOrDefault socketManager,
                    socketFactoryOrDefault socketFactory)

        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            root : 'Root,
                                            ?socketManager : IGQLServerSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLServerSocket) =
            this.UseGQLWebSockets(
                    executor,
                    (fun _ -> root), 
                    socketManagerOrDefault socketManager,
                    socketFactoryOrDefault socketFactory)