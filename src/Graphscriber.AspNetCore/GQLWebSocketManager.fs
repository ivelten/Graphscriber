namespace Graphscriber.AspNetCore

open System
open System.Threading.Tasks
open System.Net.WebSockets
open FSharp.Data.GraphQL
open FSharp.Data.GraphQL.Execution
open System.Collections.Generic
open System.Collections.Concurrent

type IGQLWebSocketManager<'Root> =
    abstract member StartSocket : IGQLWebSocket<'Root> * Executor<'Root> * 'Root -> Task

type GQLWebSocketManager<'Root>() =
    let sockets : IDictionary<Guid, IGQLWebSocket<'Root>> = 
        upcast ConcurrentDictionary<Guid, IGQLWebSocket<'Root>>()

    let disposeSocket (socket : IGQLWebSocket<'Root>) =
        sockets.Remove(socket.Id) |> ignore
        socket.Dispose()

    let sendMessage (socket : IGQLWebSocket<'Root>) (message : GQLServerMessage) = 
        async {
            if socket.State = WebSocketState.Open then
                do! socket.SendAsync(message) |> Async.AwaitTask
            else
                disposeSocket socket
        }

    let receiveMessage (executor : Executor<'Root>) (replacements : Map<string, obj>) (socket : IGQLWebSocket<'Root>) =
        socket.ReceiveAsync(executor, replacements) |> Async.AwaitTask

    let handleMessages (executor : Executor<'Root>) (root : 'Root) (socket : IGQLWebSocket<'Root>) = async {
        let send id output =
            Data (id, output)
            |> sendMessage socket
            |> Async.RunSynchronously
        let handle id =
            function
            | Stream output ->
                let unsubscriber = output |> Observable.subscribe (fun o -> send id o)
                socket.Subscribe(id, unsubscriber)
            | Deferred (data, _, output) ->
                send id data
                let unsubscriber = output |> Observable.subscribe (fun o -> send id o)
                socket.Subscribe(id, unsubscriber)
            | Direct (data, _) ->
                send id data
        try
            let mutable loop = true
            while loop do
                let! message = socket |> receiveMessage executor Map.empty
                match message with
                | Some ConnectionInit ->
                    do! sendMessage socket ConnectionAck
                | Some (Start (id, payload)) ->
                    executor.AsyncExecute(payload.ExecutionPlan, root, payload.Variables)
                    |> Async.RunSynchronously
                    |> handle id
                    do! Data (id, Dictionary<string, obj>()) |> sendMessage socket
                | Some ConnectionTerminate ->
                    do! socket.CloseAsync() |> Async.AwaitTask
                    disposeSocket socket
                    loop <- false
                | Some (ParseError (id, _)) ->
                    do! Error (id, "Invalid message type!") |> sendMessage socket
                | Some (Stop id) ->
                    socket.Unsubscribe(id)
                    do! Complete id |> sendMessage socket
                | None -> ()
        with
        | _ -> disposeSocket socket
    }

    member __.StartSocket(socket : IGQLWebSocket<'Root>, executor : Executor<'Root>, root : 'Root) =
        sockets.Add(socket.Id, socket)
        handleMessages executor root socket |> Async.StartAsTask :> Task

    interface IGQLWebSocketManager<'Root> with
        member this.StartSocket(socket, executor, root) = this.StartSocket(socket, executor, root)