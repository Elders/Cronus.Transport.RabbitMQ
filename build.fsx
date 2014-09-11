﻿#I @"./bin/tools/FAKE/tools/"
#r @"./bin/tools/FAKE/tools/FakeLib.dll"
#load @"./bin/tools/SourceLink.Fake/tools/SourceLink.fsx"

open Fake
open Fake.Git
open Fake.FSharpFormatting
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO

type System.String with member x.endswith (comp:System.StringComparison) str = 
    let newVal = x.Remove(x.Length-4) 
    newVal.EndsWith(str, comp)
let excludePaths (pathsToExclude : string list) (path: string) = pathsToExclude |> List.exists (path.endswith StringComparison.OrdinalIgnoreCase)|> not

let buildDir  = @"./bin/Release"
let release = LoadReleaseNotes "RELEASE_NOTES.md"

let projectName = "Cronus.Transport.RabbitMQ"
let projectSummary = "Message transport for Cronus with RabbitMQ"
let projectDescription = "Message transport for Cronus with RabbitMQ"
let projectAuthors = ["Nikolai Mynkow"; "Simeon Dimov";]

let packages = ["Cronus.Transport.RabbitMQ", projectDescription]
let nugetDir = "./bin/nuget"
let nugetDependencies = getDependencies "./src/Elders.Cronus.Transport.RabbitMQ/packages.config"
let nugetDependenciesFlat, _ = nugetDependencies |> List.unzip
let excludeNugetDependencies = excludePaths nugetDependenciesFlat

Target "Clean" (fun _ -> CleanDirs [buildDir])

Target "AssemblyInfo" (fun _ ->
    CreateCSharpAssemblyInfo @"./src/Elders.Cronus.Transport.RabbitMQ/Properties/AssemblyInfo.cs"
           [Attribute.Title "Elders.Cronus.Transport.RabbitMQ"
            Attribute.Description "Elders.Cronus.Transport.RabbitMQ"
            Attribute.Product "Elders.Cronus.Transport.RabbitMQ"
            Attribute.Version release.AssemblyVersion
            Attribute.InformationalVersion release.AssemblyVersion
            Attribute.FileVersion release.AssemblyVersion]
)

Target "Build" (fun _ ->
    !! @"./src/*.sln" 
        |> MSBuildRelease null "Build"
        |> Log "Build-Output: "
)

Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with OutputPath = "./src/packages" }))
)

Target "CreateNuGet" (fun _ ->
    for package,description in packages do
    
        let nugetToolsDir = nugetDir @@ "lib" @@ "net40-full"
        CleanDir nugetToolsDir

        match package with
        | p when p = projectName ->
            CopyDir nugetToolsDir (buildDir @@ ("Elders." + package)) excludeNugetDependencies
        !! (nugetToolsDir @@ "*.srcsv") |> DeleteFiles
        
        let nuspecFile = package + ".nuspec"
        NuGet (fun p ->
            {p with
                Authors = projectAuthors
                Project = package
                Description = description
                Version = release.NugetVersion
                Summary = projectSummary
                ReleaseNotes = release.Notes |> toLines
                Dependencies = nugetDependencies
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                Publish = hasBuildParam "nugetkey"
                ToolPath = "./tools/NuGet/nuget.exe"
                OutputPath = nugetDir
                WorkingDir = nugetDir }) nuspecFile
)

Target "Release" (fun _ ->
    StageAll ""
    Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)

// Dependencies
"Clean"
    ==> "RestorePackages"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "CreateNuGet"
    ==> "Release"
 
// start build
RunParameterTargetOrDefault "target" "Build"