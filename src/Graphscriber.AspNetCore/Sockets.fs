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
        let json = JsonConvert.SerializeObject(message, settings)
        let buffer = utf8Bytes json
        let segment = ArraySegment<byte>(buffer)
        socket.SendAsync(segment, WebSocketMessageType.Text, true, ct)

    let receiveMessage<'T> (settings : JsonSerializerSettings) (socket : WebSocket) (ct : CancellationToken) =
        let buffer = Array.zeroCreate 4096
        let segment = ArraySegment<byte>(buffer)
        socket.ReceiveAsync(segment, ct)
        |> continueWithResult (fun _ ->
            let message = utf8String buffer
            if isNullOrWhiteSpace message
            then None
            else JsonConvert.DeserializeObject<'T>(message, settings) |> Some)

type IGQLSocket =
    inherit IDisposable
    abstract member CloseAsync : unit -> Task
    abstract member CloseAsync : CancellationToken -> Task
    abstract member State : WebSocketState
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
    abstract member SendAsync : GQLServerMessage -> Task
    abstract member ReceiveAsync : unit -> Task<GQLClientMessage option>

type IGQLClientSocket =
    inherit IGQLSocket
    abstract member SendAsync : GQLClientMessage * CancellationToken -> Task
    abstract member ReceiveAsync : CancellationToken -> Task<GQLServerMessage option>
    abstract member SendAsync : GQLClientMessage -> Task
    abstract member ReceiveAsync : unit -> Task<GQLServerMessage option>

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

    member this.SendAsync(message: GQLServerMessage) =
        this.SendAsync(message, CancellationToken.None)

    member this.ReceiveAsync() =
        this.ReceiveAsync(CancellationToken.None)

    member __.Dispose = inner.Dispose

    member __.CloseAsync(cancellationToken) = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken)

    member __.CloseAsync() = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)

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
        member this.SendAsync(message) = this.SendAsync(message)
        member this.ReceiveAsync() = this.ReceiveAsync()
        member this.State = this.State
        member this.CloseAsync(ct) = this.CloseAsync(ct)
        member this.CloseAsync() = this.CloseAsync()
        member this.CloseStatus = this.CloseStatus
        member this.CloseStatusDescription = this.CloseStatusDescription

type [<Sealed>] GQLClientSocket (inner : WebSocket) =
    member __.SendAsync(message: GQLClientMessage, cancellationToken : CancellationToken) =
        sendMessage message GQLClientMessage.SerializationSettings inner cancellationToken

    member __.ReceiveAsync(cancellationToken : CancellationToken) =
        receiveMessage<GQLServerMessage> GQLServerMessage.SerializationSettings inner cancellationToken

    member this.SendAsync(message: GQLClientMessage) =
        this.SendAsync(message, CancellationToken.None)

    member this.ReceiveAsync() =
        this.ReceiveAsync(CancellationToken.None)

    member __.Dispose = inner.Dispose

    member __.CloseAsync(cancellationToken) = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken)

    member __.CloseAsync() = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)

    member __.State = inner.State

    member __.CloseStatus = inner.CloseStatus |> Option.ofNullable

    member __.CloseStatusDescription = inner.CloseStatusDescription |> Option.ofObj

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    interface IGQLClientSocket with
        member this.SendAsync(message, ct) = this.SendAsync(message, ct)
        member this.ReceiveAsync(ct) = this.ReceiveAsync(ct)
        member this.SendAsync(message) = this.SendAsync(message)
        member this.ReceiveAsync() = this.ReceiveAsync()
        member this.State = this.State
        member this.CloseAsync(ct) = this.CloseAsync(ct)
        member this.CloseAsync() = this.CloseAsync()
        member this.CloseStatus = this.CloseStatus
        member this.CloseStatusDescription = this.CloseStatusDescription

[<AllowNullLiteral>]
type IGQLServerSocketManager<'Root> =
    abstract member StartSocket : IGQLServerSocket * Executor<'Root> * 'Root -> unit

[<AllowNullLiteral>]
type GQLServerSocketManager<'Root>() =
    let sockets : IDictionary<Guid, IGQLServerSocket> = 
        upcast ConcurrentDictionary<Guid, IGQLServerSocket>()

    let disposeSocket (socket : IGQLServerSocket) =
        sockets.Remove(socket.Id) |> ignore
        socket.Dispose()

    let sendMessage (socket : IGQLServerSocket) (message : GQLServerMessage) = 
        if socket.State = WebSocketState.Open then
            socket.SendAsync(message, CancellationToken.None)
        else
            upcast Task.Factory.StartNew(fun _ -> disposeSocket socket)

    let receiveMessage (socket : IGQLServerSocket) =
        socket.ReceiveAsync(CancellationToken.None)

    let handleMessages (executor : Executor<'Root>) (root : 'Root) (socket : IGQLServerSocket) =
        let send id output =
            Data (id, output)
            |> sendMessage socket
            |> ignore
        let handle id =
            function
            | Stream output ->
                let unsubscriber = output |> Observable.subscribe (send id)
                socket.Subscribe(id, unsubscriber)
            | Deferred (data, _, output) ->
                send id data
                let unsubscriber = output |> Observable.subscribe (send id)
                socket.Subscribe(id, unsubscriber)
            | Direct (data, _) ->
                send id data
        try
            let mutable loop = true
            while loop do
                socket 
                |> receiveMessage
                |> continueWithResult (fun message ->
                    match message with
                    | Some ConnectionInit ->
                        sendMessage socket ConnectionAck
                        |> ignore
                    | Some (Start (id, payload)) ->
                        executor.AsyncExecute(payload.Query, root, payload.Variables)
                        |> Async.StartAsTask
                        |> continueWithResult (handle id)
                        |> ignore
                    | Some ConnectionTerminate ->
                        socket.CloseAsync(CancellationToken.None)
                        |> continueWith (fun _ ->
                            disposeSocket socket
                            loop <- false)
                        |> wait // We should wait until the socket is disposed, so the loop can terminate after that
                    | Some (Stop id) ->
                        socket.Unsubscribe(id)
                        Complete id 
                        |> sendMessage socket
                        |> ignore
                    | None -> ())
                |> wait // Every loop should wait at least the message to be received, or the socket to be closed
        with
        | _ -> disposeSocket socket


    member __.StartSocket(socket : IGQLServerSocket, executor : Executor<'Root>, root : 'Root) =
        sockets.Add(socket.Id, socket)
        handleMessages executor root socket

    interface IGQLServerSocketManager<'Root> with
        member this.StartSocket(socket, executor, root) = this.StartSocket(socket, executor, root)