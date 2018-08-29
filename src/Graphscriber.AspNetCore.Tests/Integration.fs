module Graphscriber.AspNetCore.Tests.Integration

open Expecto
open Graphscriber.AspNetCore
open Graphscriber.AspNetCore.Tests.Helpers
open System.Net
open System.Net.WebSockets

let endpointTests server =
    testList "Endpoint tests" [
        testCase "Healthcheck test" <| fun _ ->
            use client = createHttpClient server
            client
            |> get "/healthcheck"
            |> check [ 
                statusCodeEquals HttpStatusCode.OK
                contentEquals "Service is running." 
            ]
    ]

let webSocketTests server =
    testList "WebSocket tests" [
        testCase "Should not connect if not using expected protocol" <| fun _ ->
            let client = createWebSocketClient server
            let connection = connect client
            connection
            |> waitMessage
            |> check [
                stateEquals WebSocketState.CloseReceived
                closeStatusEquals (Some WebSocketCloseStatus.ProtocolError)
                closeStatusDescriptionEquals (Some "Server only supports graphql-ws protocol.") 
            ] 
        testCase "Should be able to connect if using expected protocol" <| fun _ ->
            let client = createWebSocketClient server
            let connection =
                client
                |> setProtocol "graphql-ws"
                |> connect
            connection
            |> stateEquals WebSocketState.Open 
        testCase "Should be able to start a GQL connection" <| fun _ ->
            let client = createWebSocketClient server
            let connection =
                client
                |> setProtocol "graphql-ws"
                |> connect
            connection
            |> sendMessage ConnectionInit
            |> receiveMessage
            |> equals (Some ConnectionAck)
        
    ]

let runIntegrationTests config args server =
    [ endpointTests; webSocketTests ]
    |> List.map (fun test -> test server)
    |> testList "Integration tests"
    |> runTestsWithArgs config args