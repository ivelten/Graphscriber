namespace Graphscriber.AspNetCore.Tests.WebApp

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting

module Program =
    let private exitCode = 0

    let [<Literal>] BaseAddress = "localhost:8084"

    let createWebHostBuilder args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .UseUrls(sprintf "http://%s" BaseAddress)

    [<EntryPoint>]
    let main args =
        createWebHostBuilder(args)
            .Build()
            .Run()
        exitCode
