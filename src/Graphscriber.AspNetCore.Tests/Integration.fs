module Graphscriber.AspNetCore.Tests.Integration

open Expecto
open Graphscriber.AspNetCore.Tests.WebApp
open Microsoft.AspNetCore.TestHost
open Graphscriber.AspNetCore.Tests.Helpers
open System.Net

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
    testList "WebSocket tests" [
         ]