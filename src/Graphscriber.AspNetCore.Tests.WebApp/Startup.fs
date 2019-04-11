namespace Graphscriber.AspNetCore.Tests.WebApp

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.Extensions.Logging
open System
open Graphscriber.AspNetCore

type Startup() =

    let webApp =
        choose
            [ route "/healthcheck" >=> text "Service is running." ]

    abstract member ConfigureServices : IServiceCollection -> unit

    default __.ConfigureServices(services : IServiceCollection) =
        services
            .AddGiraffe()
            .AddGQLServerSocketManager<Root>() |> ignore

    member __.Configure(app : IApplicationBuilder, _ : IHostingEnvironment) =
        let errorHandler (ex : Exception) (log : ILogger) =
            log.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
            clearResponse >=> setStatusCode 500
        app
            .UseGiraffeErrorHandler(errorHandler)
            .UseGQLWebSockets(Schema.executor, (fun _ -> { RequestId = Guid.NewGuid() }))
            .UseGiraffe(webApp)

type TestStartup() =
    inherit Startup()
    let connectionHandler (connectionParams : Map<string, obj> option) =
        match connectionParams with
        | Some p ->
            match Map.tryFind "token" p with
            | Some t -> 
                if t.ToString() = "f1ec4251-831e-4889-83dd-99d928e13778"
                then Accept
                else Reject "Invalid connection token parameter."
            | None -> Reject "Expected a token connection parameter, but none was sent."
        | None -> Reject "Expected connection parameters, but none was sent."
    override __.ConfigureServices(services : IServiceCollection) =
        services
            .AddGiraffe()
            .AddGQLServerSocketManager<Root>(connectionHandler) |> ignore