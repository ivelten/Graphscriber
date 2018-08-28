namespace Graphscriber.AspNetCore

open System
open System.Net.WebSockets
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Newtonsoft.Json
open FSharp.Data.GraphQL
open FSharp.Data.GraphQL.Execution

[<AutoOpen>]
module internal WebSocketUtils =
    let sendMessage message (settings : JsonSerializerSettings) (socket : WebSocket) (ct : CancellationToken) =
        async {
            let json = JsonConvert.SerializeObject(message, settings)
            let buffer = utf8Bytes json
            let segment = ArraySegment<byte>(buffer)
            do! socket.SendAsync(segment, WebSocketMessageType.Text, true, ct) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task

    let receiveMessage<'T> (settings : JsonSerializerSettings) (socket : WebSocket) (ct : CancellationToken) =
        async {
            let buffer = Array.zeroCreate 4096
            let segment = ArraySegment<byte>(buffer)
            do! socket.ReceiveAsync(segment, ct)
                |> Async.AwaitTask
                |> Async.Ignore
            let message = utf8String buffer
            if isNullOrWhiteSpace message
            then
                return None
            else
                return JsonConvert.DeserializeObject<'T>(message, settings) |> Some
        } |> Async.StartAsTask

type IGQLSocket =
    inherit IDisposable
    abstract member State : WebSocketState
    abstract member CloseAsync : CancellationToken -> Task
    abstract member CloseStatus : WebSocketCloseStatus option
    abstract member CloseStatusDescription : string option

type IGQLServerSocket =
    inherit IGQLSocket
    abstract member Subscribe : string * IDisposable -> unit
    abstract member Unsubscribe : string -> unit
    abstract member UnsubscribeAll : unit -> unit
    abstract member Id : Guid
    abstract member SendAsync : GQLServerMessage * CancellationToken -> Task
    abstract member ReceiveAsync : CancellationToken -> Task<GQLClientMessage option>

type IGQLClientSocket =
    inherit IGQLSocket
    abstract member SendAsync : GQLClientMessage * CancellationToken -> Task
    abstract member ReceiveAsync : CancellationToken -> Task<GQLServerMessage option>

type [<Sealed>] GQLServerSocket (inner : WebSocket) =
    let subscriptions : IDictionary<string, IDisposable> = 
        upcast ConcurrentDictionary<string, IDisposable>()

    let id = System.Guid.NewGuid()

    member __.Subscribe(id : string, unsubscriber : IDisposable) =
        subscriptions.Add(id, unsubscriber)

    member __.Unsubscribe(id : string) =
        match subscriptions.ContainsKey(id) with
        | true ->
            subscriptions.[id].Dispose()
            subscriptions.Remove(id) |> ignore
        | false -> ()

    member __.UnsubscribeAll() =
        subscriptions
        |> Seq.iter (fun x -> x.Value.Dispose())
        subscriptions.Clear()
        
    member __.Id = id

    member __.SendAsync(message: GQLServerMessage, cancellationToken) =
        sendMessage message GQLServerMessage.SerializationSettings inner cancellationToken

    member __.ReceiveAsync(cancellationToken) =
        receiveMessage<GQLClientMessage> GQLClientMessage.SerializationSettings inner cancellationToken

    member __.Dispose = inner.Dispose

    member __.CloseAsync(cancellationToken) = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken)

    member __.State = inner.State

    member __.CloseStatus = inner.CloseStatus |> Option.ofNullable

    member __.CloseStatusDescription = inner.CloseStatusDescription |> Option.ofObj

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    interface IGQLServerSocket with
        member this.Subscribe(id, unsubscriber) = this.Subscribe(id, unsubscriber)
        member this.Unsubscribe(id) = this.Unsubscribe(id)
        member this.UnsubscribeAll() = this.UnsubscribeAll()
        member this.Id = this.Id
        member this.SendAsync(message, ct) = this.SendAsync(message, ct)
        member this.ReceiveAsync(ct) = this.ReceiveAsync(ct)
        member this.State = this.State
        member this.CloseAsync(ct) = this.CloseAsync(ct)
        member this.CloseStatus = this.CloseStatus
        member this.CloseStatusDescription = this.CloseStatusDescription

type [<Sealed>] GQLClientSocket (inner : WebSocket) =
    member __.SendAsync(message: GQLClientMessage, cancellationToken : CancellationToken) =
        sendMessage message GQLClientMessage.SerializationSettings inner cancellationToken

    member __.ReceiveAsync(cancellationToken : CancellationToken) =
        receiveMessage<GQLServerMessage> GQLServerMessage.SerializationSettings inner cancellationToken

    member __.Dispose = inner.Dispose

    member __.CloseAsync(cancellationToken) = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken)

    member __.State = inner.State

    member __.CloseStatus = inner.CloseStatus |> Option.ofNullable

    member __.CloseStatusDescription = inner.CloseStatusDescription |> Option.ofObj

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    interface IGQLClientSocket with
        member this.SendAsync(message, ct) = this.SendAsync(message, ct)
        member this.ReceiveAsync(ct) = this.ReceiveAsync(ct)
        member this.State = this.State
        member this.CloseAsync(ct) = this.CloseAsync(ct)
        member this.CloseStatus = this.CloseStatus
        member this.CloseStatusDescription = this.CloseStatusDescription

[<AllowNullLiteral>]
type IGQLServerSocketManager<'Root> =
    abstract member StartSocket : IGQLServerSocket * Executor<'Root> * 'Root -> Task

[<AllowNullLiteral>]
type GQLServerSocketManager<'Root>() =
    let sockets : IDictionary<Guid, IGQLServerSocket> = 
        upcast ConcurrentDictionary<Guid, IGQLServerSocket>()

    let disposeSocket (socket : IGQLServerSocket) =
        sockets.Remove(socket.Id) |> ignore
        socket.Dispose()

    let sendMessage (socket : IGQLServerSocket) (message : GQLServerMessage) = 
        async {
            if socket.State = WebSocketState.Open then
                do! socket.SendAsync(message, CancellationToken.None) |> Async.AwaitTask
            else
                disposeSocket socket
        }

    let receiveMessage (socket : IGQLServerSocket) =
        socket.ReceiveAsync(CancellationToken.None) |> Async.AwaitTask

    let handleMessages (executor : Executor<'Root>) (root : 'Root) (socket : IGQLServerSocket) = async {
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
                let! message = socket |> receiveMessage
                match message with
                | Some ConnectionInit ->
                    do! sendMessage socket ConnectionAck
                | Some (Start (id, payload)) ->
                    executor.AsyncExecute(payload.Query, root, payload.Variables)
                    |> Async.RunSynchronously
                    |> handle id
                    do! Data (id, Dictionary<string, obj>()) |> sendMessage socket
                | Some ConnectionTerminate ->
                    do! socket.CloseAsync(CancellationToken.None) |> Async.AwaitTask
                    disposeSocket socket
                    loop <- false
                | Some (Stop id) ->
                    socket.Unsubscribe(id)
                    do! Complete id |> sendMessage socket
                | None -> ()
        with
        | _ -> disposeSocket socket;
    }

    member __.StartSocket(socket : IGQLServerSocket, executor : Executor<'Root>, root : 'Root) =
        sockets.Add(socket.Id, socket)
        handleMessages executor root socket |> Async.StartAsTask :> Task

    interface IGQLServerSocketManager<'Root> with
        member this.StartSocket(socket, executor, root) = this.StartSocket(socket, executor, root)