module Graphscriber.AspNetCore.Tests.Integration

open Expecto
open Graphscriber.AspNetCore
open Graphscriber.AspNetCore.Tests.Helpers
open System.Net
open System.Net.WebSockets
open FSharp.Data.GraphQL.Execution
open System
open Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http

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

let mutable connection : GQLClientConnection = null
let buyCoffeeDate = DateTime.Now.AddSeconds(float 5)
let workMeetingStartDate = DateTime.Today.AddDays(float 2).AddHours(float 9)
let workMeetingEndDate = DateTime.Today.AddDays(float 2).AddHours(float 10)
let workMeetingReminderDate = DateTime.Now.AddSeconds(float 5)

let webSocketTests server =
    testList "WebSocket tests" [
        test "Should not be able to open a socket if not using expected protocol" {
            let client = createWebSocketClient server
            let connection = connect client
            connection
            |> waitMessage
            |> check [
                stateEquals WebSocketState.CloseReceived
                closeStatusEquals (Some WebSocketCloseStatus.ProtocolError)
                closeStatusDescriptionEquals (Some "Server only supports graphql-ws protocol.") ]
        }
        testSequenced <| testList "Connection tests" [
            test "Should be able to open a socket if using expected protocol" {
                let client = createWebSocketClient server
                connection <-
                    client
                    |> setProtocol "graphql-ws"
                    |> connect
                connection
                |> stateEquals WebSocketState.Open
            }
            test "Should reject any start operation if no GQL connection was made" {
                let query = """query GetIncomingReminders {
                    incomingReminders {
                        ... on Reminder {
                            subject
                            time
                        }
                        ... on Appointment {
                            subject
                            location
                            startTime
                            endTime
                            reminder {
                                time
                            }
                        }
                    }
                }"""
                connection
                |> sendMessage (Start ("0", { Query = query; Variables = Map.empty }))
                |> receiveMessage
                |> isSome
                |> equals (Error (Some "0", "A connection has not been started. Expected a GQL_CONNECTION_INIT before this operation."))
            }
            test "Should reject any connection terminate if no GQL connection was made" {
                connection
                |> sendMessage ConnectionTerminate
                |> receiveMessage
                |> isSome
                |> equals (Error (None, "A connection has not been started. Expected a GQL_CONNECTION_INIT before this operation."))
            }
            test "Should reject any stop operation if no GQL connection was made" {
                connection
                |> sendMessage (Stop "0")
                |> receiveMessage
                |> isSome
                |> equals (Error (Some "0", "A connection has not been started. Expected a GQL_CONNECTION_INIT before this operation."))
            }
            test "Should reject a GQL connection when no parameters are sent" {
                connection
                |> sendMessage (ConnectionInit { ConnectionParams = None })
                |> receiveMessage
                |> isSome
                |> equals (ConnectionError "Expected connection parameters, but none was sent.")
            }
            test "Should reject a GQL connection when no token is sent" {
                connection
                |> sendMessage (ConnectionInit { ConnectionParams = Some (Map.empty) })
                |> receiveMessage
                |> isSome
                |> equals (ConnectionError "Expected a token connection parameter, but none was sent.")
            }
            test "Should reject a GQL connection when an invalid token parameter is sent" {
                connection
                |> sendMessage (ConnectionInit { ConnectionParams = Some (Map.ofList [ "token", upcast "" ]) })
                |> receiveMessage
                |> isSome
                |> equals (ConnectionError "Invalid connection token parameter.")
            }
            test "Should be able to start a GQL connection when a valid token parameter is sent" {
                connection
                |> sendMessage (ConnectionInit { ConnectionParams = Some (Map.ofList [ "token", upcast "f1ec4251-831e-4889-83dd-99d928e13778" ]) })
                |> receiveMessage
                |> isSome
                |> equals ConnectionAck
            }
            test "Should be able to do a mutation (insert a reminder)" {
                let query = """mutation InsertReminder ($subject : String!, $time : Date!) {
                    addReminder (subject : $subject, time : $time) {
                        subject
                        time
                    }
                }"""
                let payload = 
                    { Query = query
                      Variables = Map.ofList 
                        [ "subject", box "Buy coffee"
                          "time", upcast buyCoffeeDate ] }
                let expectedData = 
                    NameValueLookup.ofList 
                        [ "addReminder", upcast NameValueLookup.ofList 
                            [ "subject", upcast "Buy coffee"
                              "time", upcast buyCoffeeDate ] ]
                connection
                |> sendMessage (Start ("1", payload))
                |> receiveMessage
                |> isSome
                |> isData "1" expectedData
            }
            test "Should be able to do a mutation (insert an appointment)" {
                let query = """mutation InsertAppointment ($subject : String!, $location : String!, $startTime : Date!, $endTime : Date!, $reminder : Date) {
                    addAppointment (subject : $subject, location : $location, startTime : $startTime, endTime : $endTime, reminder : $reminder) {
                        subject
                        location
                        startTime
                        endTime
                        reminder {
                            time
                        }
                    }
                }"""
                let payload = 
                    { Query = query
                      Variables = Map.ofList 
                        [ "subject", box "Work's meeting"
                          "location", upcast "Work's meeting room"
                          "startTime", upcast workMeetingStartDate
                          "endTime", upcast workMeetingEndDate
                          "reminder", upcast workMeetingReminderDate ] }
                let expectedData = 
                    NameValueLookup.ofList 
                        [ "addAppointment", upcast NameValueLookup.ofList 
                            [ "subject", box "Work's meeting"
                              "location", upcast "Work's meeting room"
                              "startTime", upcast workMeetingStartDate
                              "endTime", upcast workMeetingEndDate
                              "reminder", upcast NameValueLookup.ofList 
                                [ "time", upcast workMeetingReminderDate ] ] ]
                connection
                |> sendMessage (Start ("2", payload))
                |> receiveMessage
                |> isSome
                |> isData "2" expectedData
            }
            test "Should be able to do a query" {
                let query = """query GetIncomingReminders {
                    incomingReminders {
                        ... on Reminder {
                            subject
                            time
                        }
                        ... on Appointment {
                            subject
                            location
                            startTime
                            endTime
                            reminder {
                                time
                            }
                        }
                    }
                }"""
                let expectedData = 
                    NameValueLookup.ofList 
                        [ "incomingReminders", upcast [
                              box <| NameValueLookup.ofList [
                                  "subject", upcast "Buy coffee"
                                  "time", upcast buyCoffeeDate ]
                              upcast NameValueLookup.ofList [
                                  "subject", upcast "Work's meeting"
                                  "location", upcast "Work's meeting room"
                                  "startTime", upcast workMeetingStartDate
                                  "endTime", upcast workMeetingEndDate
                                  "reminder", upcast NameValueLookup.ofList [
                                      "time", upcast workMeetingReminderDate ] ] ] ]
                connection
                |> sendMessage (Start ("3", { Query = query; Variables = Map.empty }))
                |> receiveMessage
                |> isSome
                |> isData "3" expectedData
            }
            test "Should be able to subscribe" {
                let query = """subscription SubscribeToReminders {
                    incomingReminders {
                        ... on Reminder {
                            subject
                            time
                        }
                        ... on Appointment {
                            subject
                            location
                            startTime
                            endTime
                            reminder {
                                time
                            }
                        }
                    }
                }"""
                let expectedData1 = 
                    NameValueLookup.ofList
                        [ "subject", upcast "Buy coffee"
                          "time", upcast buyCoffeeDate ]
                let expectedData2 = 
                    NameValueLookup.ofList
                        [ "subject", upcast "Work's meeting"
                          "location", upcast "Work's meeting room"
                          "startTime", upcast workMeetingStartDate
                          "endTime", upcast workMeetingEndDate
                          "reminder", upcast NameValueLookup.ofList 
                            [ "time", upcast workMeetingReminderDate ] ]
                connection
                |> sendMessage (Start ("4", { Query = query; Variables = Map.empty }))
                |> receiveMessage
                |> isSome
                |> isData "4" expectedData1
                connection
                |> receiveMessage
                |> isSome
                |> isData "4" expectedData2
            }
            test "Should be able to unsubscribe" {
                connection
                |> sendMessage (Stop "4")
                |> receiveMessage
                |> isSome
                |> equals (Complete "4")
            }
            test "Should be able to end connection" {
                connection
                |> sendMessage (ConnectionTerminate)
                |> receiveMessage
                |> isNone
            }
        ]
    ]

let runIntegrationTests config args server =
    [ endpointTests; webSocketTests ]
    |> List.map (fun test -> test server)
    |> testList "Integration tests"
    |> runTestsWithArgs config args