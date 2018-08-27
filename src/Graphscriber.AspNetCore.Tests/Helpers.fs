module Graphscriber.AspNetCore.Tests.Helpers

open System.Net.Http
open Microsoft.AspNetCore.TestHost
open Graphscriber.AspNetCore.Tests.WebApp
open Graphscriber.AspNetCore
open Expecto
open System
open System.Threading
open System.Collections.Concurrent
open System.Net.WebSockets

[<AllowNullLiteral>]
type GQLClientConnection(socket : WebSocket) =
    let socket = new GQLClientSocket(socket)
    let receivedMessages = ConcurrentBag<GQLServerMessage>()
    let mutable receiving = true
    let mre = new ManualResetEvent(false)
    let receiver =
        async {
             while receiving do
                let! message = socket.ReceiveAsync() |> Async.AwaitTask
                match message with
                | Some m -> receivedMessages.Add(m); mre.Set() |> ignore
                | None -> ()
        }
    do socket.ReceiveAsync() |> Async.AwaitTask |> Async.Ignore |> Async.RunSynchronously
    
    member __.SendMessage(message : GQLClientMessage) =
        socket.SendAsync(message) |> Async.AwaitTask |> Async.RunSynchronously

    member __.ReceivedMessages = receivedMessages |> Seq.map id

    member __.WaitMessage() = 
        let success = TimeSpan.FromSeconds(float 30) |> mre.WaitOne
        mre.Reset() |> ignore
        success

    member __.SocketCloseStatus = socket.CloseStatus

    member __.SocketCloseStatusDescription = socket.CloseStatusDescription

    member __.SocketState = socket.State

    member __.Dispose() =
        receiving <- false
        Async.RunSynchronously receiver
        mre.Dispose()
        socket.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

let get (client : HttpClient) (uri : string) =
    client.GetAsync(uri) 
    |> Async.AwaitTask 
    |> Async.RunSynchronously

let connect (socket : WebSocketClient) =
    let uri = Uri(sprintf "ws://%s" Program.BaseAddress)
    let inner =
        socket.ConnectAsync(uri, CancellationToken.None)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    new GQLClientConnection(inner)

let send (message : GQLClientMessage) (connection : GQLClientConnection) =
    connection.SendMessage(message); connection

let check checks item =
    checks |> Seq.iter (fun check -> check item)

let statusCodeEquals expected (response : HttpResponseMessage) =
    let actual = response.StatusCode
    Expect.equal actual expected "Unexpected HTTP status code"

let contentEquals expected (response : HttpResponseMessage) =
    let actual = response.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
    Expect.equal actual expected "Unexpected HTTP response content"

let equals expected actual =
    Expect.equal actual expected "Unexpected value"

let contains element sequence =
    Expect.contains sequence element "Sequence does not contain expected element"

let containsMessage message (connection : GQLClientConnection) =
    if not (connection.WaitMessage())
    then failwith "Timeout waiting for a message from the socket."
    contains message connection.ReceivedMessages

let stateEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketState

let closeStatusEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketCloseStatus

let closeStatusDescriptionEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketCloseStatusDescription