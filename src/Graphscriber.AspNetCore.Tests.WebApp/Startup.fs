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
            [ route "/ping" >=> text "Service is running." ]

    member __.ConfigureServices(services : IServiceCollection) =
        services.AddGiraffe() |> ignore

    member __.Configure(app : IApplicationBuilder, _ : IHostingEnvironment) =
        let errorHandler (ex : Exception) (log : ILogger) =
            log.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
            clearResponse >=> setStatusCode 500
        app
            .UseGiraffeErrorHandler(errorHandler)
            .UseGQLWebSockets(Schema.executor, (fun _ -> { RequestId = Guid.NewGuid() }))
            .UseGiraffe(webApp)