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
    member __.Invoke(ctx : HttpContext) : Task =
        if ctx.WebSockets.IsWebSocketRequest
        then
            ctx.WebSockets.AcceptWebSocketAsync("graphql-ws")
            |> continueWithResult (fun socket ->
                if not (ctx.WebSockets.WebSocketRequestedProtocols.Contains(socket.SubProtocol))
                then
                    socket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Server only supports graphql-ws protocol.", ctx.RequestAborted)
                    |> wait
                else
                    use socket = socketFactory socket
                    let root = rootFactory socket
                    socketManager.StartSocket(socket, executor, root))
            |> ignoreResult
        else
            next.Invoke(ctx)