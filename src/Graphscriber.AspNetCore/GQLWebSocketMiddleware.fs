namespace Graphscriber.AspNetCore

open System.Threading.Tasks
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open FSharp.Data.GraphQL

type GQLWebSocketMiddleware<'Root>(next : RequestDelegate, 
                                       executor : Executor<'Root>, 
                                       getRoot : IGQLWebSocket<'Root> -> 'Root, 
                                       socketManager : IGQLWebSocketManager<'Root>,
                                       ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
    let socketFactory = defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))

    member __.Invoke(ctx : HttpContext) =
        async {
            match ctx.WebSockets.IsWebSocketRequest with
            | true ->
                let! socket = ctx.WebSockets.AcceptWebSocketAsync("graphql-ws") |> Async.AwaitTask
                use socket = socketFactory socket
                do! socketManager.StartSocket(socket, executor, getRoot socket) |> Async.AwaitTask
            | false ->
                do! next.Invoke(ctx) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task