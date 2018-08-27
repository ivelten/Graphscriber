module Graphscriber.AspNetCore.Tests.Integration

open Expecto
open Graphscriber.AspNetCore.Tests.WebApp
open Microsoft.AspNetCore.TestHost
open Graphscriber.AspNetCore
open Graphscriber.AspNetCore.Tests.Helpers
open System.Net
open System.Net.WebSockets

let private server = new TestServer(Program.createWebHostBuilder [||])
let private client = server.CreateClient()
let private socket = server.CreateWebSocketClient()

[<Tests>]
let endpointTests =

    testList "Endpoint tests" [

        testCase "Healthcheck test" <| fun _ -> 
            get client "/healthcheck" |> check [ 
                statusCodeEquals HttpStatusCode.OK
                contentEquals "Service is running." ] ]

[<Tests>]
let webSocketTests =
    testSequenced <| testList "WebSocket tests" [

            testCase "Should not connect if not using expected protocol" <| fun _ ->
                connect socket |> check [
                    stateEquals WebSocketState.Closed
                    closeStatusEquals (Some WebSocketCloseStatus.ProtocolError)
                    closeStatusDescriptionEquals (Some "Server only supports graphql-ws protocol.") ]

            testCase "Should be able to connect if using expected protocol" <| fun _ ->
                socket.SubProtocols.Add("graphql-ws")
                connect socket
                |> stateEquals WebSocketState.Open ]