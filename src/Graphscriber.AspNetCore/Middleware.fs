namespace Graphscriber.AspNetCore

open System.Threading.Tasks
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open FSharp.Data.GraphQL

type GQLWebSocketMiddleware<'Root>(next : RequestDelegate, 
                                   executor : Executor<'Root>, 
                                   rootFactory : IGQLServerSocket -> 'Root, 
                                   socketManager : IGQLServerSocketManager<'Root>,
                                   socketFactory : WebSocket -> IGQLServerSocket) =
    member __.Invoke(ctx : HttpContext) =
        async {
            match ctx.WebSockets.IsWebSocketRequest with
            | true ->
                let! socket = ctx.WebSockets.AcceptWebSocketAsync("graphql-ws") |> Async.AwaitTask
                use socket = socketFactory socket
                let root = rootFactory socket
                do! socketManager.StartSocket(socket, executor, root) |> Async.AwaitTask
            | false ->
                do! next.Invoke(ctx) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task