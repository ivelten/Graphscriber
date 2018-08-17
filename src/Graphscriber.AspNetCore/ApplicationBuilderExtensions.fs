namespace Graphscriber.AspNetCore

open Microsoft.AspNetCore.Builder
open FSharp.Data.GraphQL
open System.Net.WebSockets

[<AutoOpen>]
module ApplicationBuilderExtensions =
    type IApplicationBuilder with
        member this.UseGQLWebSockets<'Root>(executor : Executor<'Root>,
                                            getRoot : IGQLWebSocket<'Root> -> 'Root, 
                                            socketManager : IGQLWebSocketManager<'Root>,
                                            ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
            let socketFactory = defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))                   
            this.UseWebSockets()
                .UseMiddleware<GQLWebSocketMiddleware<'Root>>(executor, getRoot, socketManager, socketFactory)