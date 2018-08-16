namespace Graphscriber.AspNetCore

open System.Threading.Tasks
open System.Net.WebSockets
open Microsoft.AspNetCore.Http
open FSharp.Data.GraphQL

type GraphQLWebSocketMiddleware<'Root>(next : RequestDelegate, 
                                       executor : Executor<'Root>, 
                                       root : 'Root, 
                                       socketManager : IGQLWebSocketManager<'Root>,
                                       ?socketFactory : WebSocket -> IGQLWebSocket<'Root>) =
    let socketFactory = defaultArg socketFactory (fun ws -> upcast new GQLWebSocket<'Root>(ws))

    member __.Invoke(ctx : HttpContext) =
        async {
            match ctx.WebSockets.IsWebSocketRequest with
            | true ->
                let! socket = ctx.WebSockets.AcceptWebSocketAsync("graphql-ws") |> Async.AwaitTask
                use socket = socketFactory socket
                socketManager.StartSocket(socket, executor, root)
            | false ->
                next.Invoke(ctx) |> ignore
        } |> Async.StartAsTask :> Task