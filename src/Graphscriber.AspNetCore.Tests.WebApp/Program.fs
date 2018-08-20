namespace Graphscriber.AspNetCore.Tests.WebApp

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting

module Program =
    let exitCode = 0

    let createWebHostBuilder args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .UseUrls("http://localhost:8084")

    [<EntryPoint>]
    let main args =
        createWebHostBuilder(args)
            .Build()
            .Run()
        exitCode
