namespace Graphscriber.AspNetCore

open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets

[<AutoOpen>]
module ApplicationBuilderExtensions =
    type IApplicationBuilder with
        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            rootFactory : IGQLWebSocket<'Root> -> 'Root, 
                                            ?socketManager : IGQLWebSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
            let socketManager = defaultArg socketManager (upcast GQLWebSocketManager())
            let socketFactory = defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))
            this.UseWebSockets()
                .UseMiddleware<GQLWebSocketMiddleware<'Root>>(executor, rootFactory, socketManager, socketFactory)

        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            root : 'Root,
                                            ?socketManager : IGQLWebSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
            let socketManager = defaultArg socketManager (upcast GQLWebSocketManager())
            let socketFactory = defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))
            this.UseGQLWebSockets(executor, (fun _ -> root), socketManager, socketFactory)