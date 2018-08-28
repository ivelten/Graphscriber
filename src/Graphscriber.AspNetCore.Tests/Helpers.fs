module Graphscriber.AspNetCore.Tests.Helpers

open System.Net.Http
open Microsoft.AspNetCore.TestHost
open Graphscriber.AspNetCore.Tests.WebApp
open Graphscriber.AspNetCore
open Expecto
open System
open System.Threading
open System.Net.WebSockets
open Microsoft.Extensions.Primitives

[<AllowNullLiteral>]
type GQLClientConnection(socket : WebSocket) =
    let socket = new GQLClientSocket(socket)
    
    member __.SendMessage(message : GQLClientMessage) =
        socket.SendAsync(message, CancellationToken.None) |> Async.AwaitTask |> Async.RunSynchronously

    member __.WaitMessage() = 
        use tokenSource = new CancellationTokenSource(30000)
        socket.ReceiveAsync(tokenSource.Token) 
        |> Async.AwaitTask 
        |> Async.RunSynchronously

    member __.SocketCloseStatus = socket.CloseStatus

    member __.SocketCloseStatusDescription = socket.CloseStatusDescription

    member __.SocketState = socket.State

    member __.Dispose() =
        socket.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

let get (uri : string) (client : HttpClient) =
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

let stateEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketState

let closeStatusEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketCloseStatus

let closeStatusDescriptionEquals expected (connection : GQLClientConnection) =
    equals expected connection.SocketCloseStatusDescription

let sendMessage message (connection : GQLClientConnection) =
    connection.SendMessage(message); connection

let receiveMessage (connection : GQLClientConnection) =
    try connection.WaitMessage()
    with ex -> failwithf "Error while waiting for a message from the socket.\n%A" ex

let waitMessage (connection : GQLClientConnection) =
    receiveMessage connection |> ignore; connection

let createServer () =
    new TestServer(Program.createWebHostBuilder [||])

let createHttpClient (server : TestServer) =
    server.CreateClient()

let createWebSocketClient (server : TestServer) =
    server.CreateWebSocketClient()

let setProtocol (protocol : string) (client : WebSocketClient) =
    client.ConfigureRequest <- fun r -> r.Headers.Add("Sec-WebSocket-Protocol", StringValues(protocol))
    client