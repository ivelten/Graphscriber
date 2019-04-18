#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.Core.ReleaseNotes
nuget Fake.DotNet.Testing.Expecto
nuget Fake.DotNet.Paket //"

#load ".fake/build.fsx/intellisense.fsx"

#if !FAKE
  #r "netstandard"
  #r "Facades/netstandard"
#endif

open Fake.Core
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

Target.create "Restore" (fun _ ->
    !! "src/**/*.fsproj"
    ++ "src/**/*.csproj"
    |> Seq.iter (DotNet.restore id))

Target.create "Build" (fun _ ->
    !! "src/**/*.fsproj"
    ++ "src/**/*.csproj"
    |> Seq.iter (DotNet.build (fun options ->
        { options with 
            Configuration = DotNet.BuildConfiguration.Release
            Common = { options.Common with 
                        CustomParams = Some "--no-restore" } })))

Target.create "Test" (fun _ ->
    !! "src/**/bin/Release/*/*Tests.dll"
    |> Expecto.run id)

let project = "Graphscriber"
let summary = "GraphQL over WebSocket implementation"
let gitOwner = "ivelten"
let gitHome = "https://github.com/" + gitOwner
let gitName = "Graphscriber"
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let pack id =
    Shell.cleanDir <| sprintf "nuget/%s.%s" project id
    Paket.pack(fun p ->
        { p with
            Version = release.NugetVersion
            OutputPath = sprintf "nuget/%s.%s" project id
            TemplateFile = sprintf "src/%s.%s/%s.%s.fsproj.paket.template" project id project id
            IncludeReferencedProjects = true
            MinimumFromLockFile = true
        })

let publish id =
    pack id
    Paket.push (fun options ->
        { options with
            WorkingDir = sprintf "nuget/%s.%s" project id
            PublishUrl = "https://www.nuget.org/api/v2/package" })

Target.create "PackAspNetCore" (fun _ -> pack "AspNetCore")

Target.create "PublishAspNetCore" (fun _ -> publish "AspNetCore")

Target.create "All" ignore

"Clean"
  ==> "Restore"
  ==> "Build"
  ==> "Test"
  ==> "All"

Target.runOrDefault "All"
