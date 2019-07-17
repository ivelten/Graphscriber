module Graphscriber.AspNetCore.Tests.Program

open Expecto
open Graphscriber.AspNetCore.Tests.Integration
open Graphscriber.AspNetCore.Tests.Helpers

[<EntryPoint>]
let main argv =
    use server = createServer ()
    runIntegrationTests defaultConfig argv server
