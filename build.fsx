#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.Core.ReleaseNotes
nuget Fake.DotNet.Testing.Expecto
nuget Fake.DotNet.Paket //"

#load ".fake/build.fsx/intellisense.fsx"

#if !FAKE
  #r "netstandard"
  #r "Facades/netstandard"
#endif

open System.IO
open Fake.Core
open Fake.DotNet
open Fake.DotNet.Testing
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.IO.FileSystemOperators

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
    !! "tests/**/bin/Release/*/*Tests.dll"
    |> Expecto.run id)

let project = "Graphscriber"
let summary = "GraphQL over WebSocket implementation"
let gitOwner = "ivelten"
let gitHome = "https://github.com/" + gitOwner
let gitName = "Graphscriber"
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title projectName
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion ]
    let getProjectDetails projectPath =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        projectPath,
        projectName,
        Path.GetDirectoryName(projectPath),
        (getAssemblyInfoAttributes projectName)
    let internalsVisibility (fsproj: string) =
        match fsproj with
        | "Graphscriber.AspNetCore.fsproj" -> [ AssemblyInfo.InternalsVisibleTo "Graphscriber.AspNetCore.Tests" ]
        | _ -> []
    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
            match projFileName with
            | Fsproj -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") (attributes @ internalsVisibility projFileName)
            | Csproj -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
            | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
            | Shproj -> ()
        ))

let pack id =
    Shell.cleanDir <| sprintf "nuget/%s.%s" project id
    Paket.pack(fun p ->
        { p with
            Version = release.NugetVersion
            OutputPath = sprintf "nuget/%s.%s" project id
            TemplateFile = sprintf "src/%s.%s/%s.%s.fsproj.paket.template" project id project id
            IncludeReferencedProjects = true
            MinimumFromLockFile = true
            ReleaseNotes = release.Notes |> List.reduce (+)
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
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Test"
  ==> "All"

Target.runOrDefault "All"
