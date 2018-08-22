namespace Graphscriber.AspNetCore

open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets

[<AutoOpen>]
module ApplicationBuilderExtensions =
    let private socketManagerOrDefault (socketManager : IGQLWebSocketManager<'Root> option) = 
        defaultArg socketManager (upcast GQLWebSocketManager())

    let private socketFactoryOrDefault (socketFactory : (WebSocket -> IGQLWebSocket<'Root>) option) =
        defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))

    type IApplicationBuilder with
        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            rootFactory : IGQLWebSocket<'Root> -> 'Root, 
                                            ?socketManager : IGQLWebSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
            this.UseWebSockets()
                .UseMiddleware<GQLWebSocketMiddleware<'Root>>(
                    executor,
                    rootFactory, 
                    socketManagerOrDefault socketManager,
                    socketFactoryOrDefault socketFactory)

        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            root : 'Root,
                                            ?socketManager : IGQLWebSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
            this.UseGQLWebSockets(
                    executor,
                    (fun _ -> root), 
                    socketManagerOrDefault socketManager,
                    socketFactoryOrDefault socketFactory)