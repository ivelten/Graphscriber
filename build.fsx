#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli //"

#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

Target.create "Restore" (fun _ ->
    "Graphscriber.sln"
    |> DotNet.restore id)

Target.create "Build" (fun _ ->
    "Graphscriber.sln"
    |> DotNet.build (fun options -> 
        { options with 
            Configuration = DotNet.BuildConfiguration.Release })
)

"Clean"
  ==> "Restore"
  ==> "Build"

Target.runOrDefault "Build"
