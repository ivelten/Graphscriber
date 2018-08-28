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
            if ctx.WebSockets.IsWebSocketRequest
            then
                let! socket = ctx.WebSockets.AcceptWebSocketAsync("graphql-ws") |> Async.AwaitTask
                if not (ctx.WebSockets.WebSocketRequestedProtocols.Contains(socket.SubProtocol))
                then
                    do! socket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Server only supports graphql-ws protocol.", ctx.RequestAborted) 
                        |> Async.AwaitTask
                else
                    use socket = socketFactory socket
                    let root = rootFactory socket
                    do! socketManager.StartSocket(socket, executor, root) |> Async.AwaitTask
            else
                do! next.Invoke(ctx) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task