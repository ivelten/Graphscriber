module Graphscriber.AspNetCore.Tests.Integration

open Expecto
open Graphscriber.AspNetCore
open Graphscriber.AspNetCore.Tests.Helpers
open System.Net
open System.Net.WebSockets

let endpointTests server =
    testList "Endpoint tests" [
        test "Healthcheck test" {
            use client = createHttpClient server
            client
            |> get "/healthcheck"
            |> check [ 
                statusCodeEquals HttpStatusCode.OK
                contentEquals "Service is running." 
            ] 
        } 
    ]

let webSocketTests server =
    testList "WebSocket tests" [
        test "Should not connect if not using expected protocol" {
            let client = createWebSocketClient server
            use connection = connect client
            connection
            |> waitMessage
            |> check [
                stateEquals WebSocketState.CloseReceived
                closeStatusEquals (Some WebSocketCloseStatus.ProtocolError)
                closeStatusDescriptionEquals (Some "Server only supports graphql-ws protocol.") 
            ] 
        }
        test "Should be able to connect if using expected protocol" {
            let client = createWebSocketClient server
            use connection =
                client
                |> setProtocol "graphql-ws"
                |> connect
            connection
            |> stateEquals WebSocketState.Open 
        }
        test "Should be able to start a GQL connection" {
            let client = createWebSocketClient server
            use connection =
                client
                |> setProtocol "graphql-ws"
                |> connect
            connection
            |> sendMessage ConnectionInit
            |> receiveMessage
            |> equals (Some ConnectionAck)
        }
    ]

let runIntegrationTests config args server =
    [ endpointTests; webSocketTests ]
    |> List.map (fun test -> test server)
    |> testList "Integration tests"
    |> runTestsWithArgs config args