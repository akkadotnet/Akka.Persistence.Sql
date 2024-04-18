#r "nuget: Fake.Core.ReleaseNotes"
#r "nuget: Fake.Core.Target"
#r "nuget: Fake.Dotnet.Nuget"
#r "nuget: Fake.Dotnet.Cli"
#r "nuget: Fake.DotNet.Testing.XUnit2"
#r "nuget: MSBuild.StructuredLogger"

open System
open System.IO
open System.Text
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.DotNet.NuGet.Install
open Fake.Testing.Common

//Information about the project for Nuget and Assembly info files
let configuration = DotNet.BuildConfiguration.Release

// Read release notes and version
let root = __SOURCE_DIRECTORY__ 
let solutionFile = Directory.findFirstMatchingFile "*.sln" root  // dynamically look up the solution
let buildNumber = Environment.environVarOrDefault "BUILD_NUMBER" "0"
let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else DateTime.UtcNow.Ticks.ToString())

let releaseNotes =
    File.ReadLines (root @@ "RELEASE_NOTES.md")
    |> ReleaseNotes.parse

let versionFromReleaseNotes =
    match releaseNotes.SemVer.PreRelease with
    | Some r -> Some r.Origin
    | None -> None

let versionSuffix =
    match Environment.environVarOrNone "nugetprerelease" with
    | Some "dev" -> Some preReleaseVersionSuffix
    | Some str -> Some str
    | None  -> versionFromReleaseNotes
    

// Directories
let toolsDir = root @@ "tools"
let output = root  @@ "bin"
let outputTests = root @@ "TestResults"
let outputPerfTests = root @@ "PerfResults"
let outputNuGet = output @@ "nuget"

let clean _ =
    Target.activateFinal "KillCreatedProcesses"

    Shell.cleanDir output
    Shell.cleanDir outputTests
    Shell.cleanDir outputPerfTests
    Shell.cleanDir outputNuGet


let assemblyInfo _ =
    Xml.pokeInnerText "./src/Directory.Generated.props" "//Project/PropertyGroup/VersionPrefix" releaseNotes.AssemblyVersion
    Xml.pokeInnerText "./src/Directory.Generated.props" "//Project/PropertyGroup/PackageReleaseNotes" (releaseNotes.Notes |> String.concat "\n")



let build _ =
    solutionFile
    |> DotNet.build
        (fun p ->
            { p with 
                Configuration = configuration
                }) 


//--------------------------------------------------------------------------------
// Tests targets
//--------------------------------------------------------------------------------
module internal ResultHandling =
    let checkExitCode msg : ProcessResult -> _ =
        _.ExitCode 
        >> function
            | 0 -> ()
            | _ -> failwith msg
    let (|OK|Failure|) = function
        | 0 -> OK
        | x -> Failure x

    let buildErrorMessage = function
        | OK -> None
        | Failure errorCode ->
            Some (sprintf "xUnit2 reported an error (Error Code %d)" errorCode)

    let failBuildWithMessage = function
        | DontFailBuild -> Trace.traceError
        | _ -> (fun m -> raise(FailedTestsException m))

    let failBuildIfXUnitReportedError errorLevel =
        buildErrorMessage
        >> Option.iter (failBuildWithMessage errorLevel)

let runTests _ =
    let projects =
        match (Environment.isWindows) with
        | true -> !! "./src/**/*.Tests.csproj"
                  -- "./src/**/*.Benchmark.*.csproj"
                  -- "./src/**/*.Data.Compatibility.Tests.csproj" // All of the data docker images are Linux only
                  -- "./src/Examples/**/*.csproj" // skip example projects
        | _ -> !! "./src/**/*.Tests.csproj"
               -- "./src/**/*.Benchmark.*.csproj"
               -- "./src/Examples/**/*.csproj" // skip example projects
               ++  "./src/**/*.DockerTests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
    let runSingleProject project =
        let args =
            match (hasTeamCity) with
            | true -> (sprintf "-c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none -teamcity" (outputTests))
            | false -> (sprintf "-c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none" (outputTests))
        //We use exec instead of DotNet.test as it doesn't allow for multiple loggers and runsettingsargs are not working https://github.com/fsprojects/FAKE/pull/2771
        in DotNet.exec (fun o ->
                    { o with
                        WorkingDirectory =  (Directory.GetParent project).FullName
                        Timeout =  Some(TimeSpan.FromMinutes 30.0)
                        }) "test" args
        |> ResultHandling.checkExitCode  "Test run failed" 
    projects |> Seq.iter (Trace.log)
    projects |> Seq.iter (runSingleProject)


let runBenchmarks args _ =
    let runSingleProject project =
        DotNet.exec(fun info ->
                        { info with
                            Timeout = Some (TimeSpan.FromMinutes 30.0)
                            WorkingDirectory = (Directory.GetParent project).FullName
                            }) "run" $"-c Release --project %s{project} -- %s{args}" 
        |> ResultHandling.checkExitCode "benchmark failed"
    in
    match Environment.isWindows with
    | true -> printfn "Windows not supported for benchmarks on CI, look at the README.md to run manually"
    | _ -> 
    !! "./src/**/*.Benchmarks.csproj" 
    |> Seq.iter runSingleProject

//--------------------------------------------------------------------------------
// Nuget targets
//--------------------------------------------------------------------------------
let overrideVersionSuffix (project:string) =
    match project with
    | _ -> versionSuffix // add additional matches to publish different versions for different projects in solution

let createNuget _ =
    let projects = !! "src/**/*.csproj"
                   -- "src/**/*Tests.csproj" // Don't publish unit tests
                   -- "src/**/*Tests*.csproj"
                   -- "src/**/*.Benchmark.*.csproj"
                   -- "./src/Examples/**/*.csproj" // skip example projects
    
    let runSingleProject project =
        DotNet.pack
            (fun p ->
                { p with
                    Configuration = configuration
                    NoBuild = true
                    IncludeSymbols = true
                    
                    VersionSuffix = (overrideVersionSuffix project)
                    OutputPath = Some outputNuGet }) project

    projects
    |> Seq.iter (runSingleProject)


let publishNuget _ =
    match Environment.environVarOrNone "nugetkey" with
    | None -> printfn "Skip nuget publish as no key were provided"
    | Some  apiKey -> 
        let sourceUrl = Environment.environVarOrDefault "nugetpublishurl" "https://api.nuget.org/v3/index.json"
        
        let rec publishPackage retryLeft packageFile =
            Trace.tracefn "Pushing %s Attempts left: %d" (Path.getFullName packageFile) retryLeft
            let before = Process.shouldEnableProcessTracing()
            try
                try
                    Process.setEnableProcessTracing false
                    DotNet.nugetPush
                        (fun p ->
                            { p with
                                Common = { p.Common with Timeout = Some (TimeSpan.FromMinutes 10.) }
                                PushParams = { p.PushParams with
                                                    ApiKey = Some apiKey
                                                    Source = Some sourceUrl
                                                    NoServiceEndpoint = true  } })
                        packageFile
                   
                with exn ->
                    printfn "Nuget push failed: %A" <| exn
                    if (retryLeft > 0) then (publishPackage (retryLeft-1) packageFile)
            finally
                Process.setEnableProcessTracing before
                
        printfn "Pushing nuget packages"
        let normalPackages = !! (outputNuGet @@ "*.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
        for package in normalPackages do
            publishPackage 3 package



//--------------------------------------------------------------------------------
// Restore dotnet tools
//--------------------------------------------------------------------------------
let restoreTools _ = 
    let restoreOne = 
        sprintf "restore --tool-manifest %s --verbosity d " 
        >> DotNet.exec
            (fun p ->
                { p with
                    Timeout = Some(TimeSpan.FromMinutes 10.)
                    PrintRedirectedOutput = true 
                     })
            "tool"
        >> ResultHandling.checkExitCode "dotnet tool restore failed"
    in
    !! "./.config/*.json" |> Seq.iter restoreOne
    

//--------------------------------------------------------------------------------
// Documentation
//--------------------------------------------------------------------------------

let buildDocfx _ = 
    let args =
        StringBuilder()
        |> StringBuilder.append (Path.getFullName "./docs" @@ "docfx.json" )
        // this fails if there are warnings in library projects
        //|> StringBuilder.append ("--warningsAsErrors")
        |> StringBuilder.toText in 
    DotNet.exec(fun info ->
            { info with
                Timeout = Some (System.TimeSpan.FromMinutes 45.0) (* Reasonably long-running task. *)})         
            "docfx" args 
    |> ResultHandling.checkExitCode "docfx failed"


let serveDocfx _ =
    let port =
        Environment.GetCommandLineArgs()
        |> Seq.tryLast
        |> Option.bind (fun arg -> try Some (int arg) with _ -> None) 
        |> Option.defaultValue 8100 in 
    let args = sprintf "serve %s --port %d" (Path.getFullName "./docs" @@ "_site" ) port in 
    DotNet.exec(fun info ->
            { info with
                Timeout = Some (System.TimeSpan.FromMinutes 45.0) (* Reasonably long-running task. *)})         
            "docfx" args 
    |> ResultHandling.checkExitCode "docfx serve failed"
    
//--------------------------------------------------------------------------------
// JetBrain targets
//--------------------------------------------------------------------------------

let inspectCode _ =
    DotNet.exec
        (fun p ->
            { p with
                Timeout = Some(TimeSpan.FromMinutes 10.) })
        "jb" "inspectcode Akka.Persistence.Sql.sln --build --swea --properties=\"Configuration=Release\" --telemetry-optout --format=\"Html;Xml;Text\" --output=\"TestResults/Akka.Persistence.Sql.jb\""
    |> ignore 


let cleanupCode _ =
    DotNet.exec
        (fun p ->
            { p with
                Timeout = Some(TimeSpan.FromMinutes 10.) })
        "jb"  "cleanupcode Akka.Persistence.Sql.sln --profile=\"Akka.NET\" --properties=\"Configuration=Release\" --telemetry-optout"
        |> ignore 

//--------------------------------------------------------------------------------
// Cleanup
//--------------------------------------------------------------------------------
let killCreatedProcesses _ =
    Trace.log "Shutting down dotnet build-server"
    DotNet.exec (fun p ->
                    { p with
                        WorkingDirectory = root
                        Timeout = Some(TimeSpan.FromMinutes 2.) })
            "build-server" "shutdown"
    |> ResultHandling.checkExitCode "dotnet build-server shutdown failed"

//--------------------------------------------------------------------------------
// Help
//--------------------------------------------------------------------------------
let help _ =
    List.iter printfn [
      "usage:"
      "./build.[sh|cmd] [target]"
      ""
      " Targets for building:"
      " * Build         Builds"
      " * Nuget         Create and optionally publish nugets packages"
      " * RunTests      Runs tests"
      " * NBench        Runs benchmarks"
      " * All           Builds, run tests, creates and optionally publish nuget packages"
      ""
      " Other Targets"
      " * Help       Display this help"
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------
let initTargets () =
    Target.create "BuildRelease" ignore
    Target.create "All" ignore
    Target.create "Nuget" ignore
    Target.create "Clean" clean
    Target.create "AssemblyInfo" assemblyInfo
    Target.create "Build" build
    Target.create "RunTests" runTests
    Target.create "GenerateBench" (runBenchmarks "generate")
    Target.create "NBench" (runBenchmarks $"--filter=\"*\" --artifacts=\"%s{outputPerfTests}\"")
    Target.create "PublishNuget" publishNuget
    Target.create "CreateNuget" createNuget
    Target.create "RestoreTools" restoreTools
    Target.create "DocFx" buildDocfx
    Target.create "ServeDocFx" serveDocfx
    Target.create "InspectCode" inspectCode
    Target.create "CleanupCode" cleanupCode
    Target.createFinal "KillCreatedProcesses" killCreatedProcesses
    Target.create "Help" help
    // build dependencies
    "Clean" ==> "AssemblyInfo" ==> "Build" ==> "InspectCode" ==> "BuildRelease" |> ignore

    // tests dependencies
    "Build" ==> "RunTests" |> ignore

    // nuget dependencies
    "Clean" ==> "Build" ==> "CreateNuget" |> ignore
    "CreateNuget" ==> "PublishNuget" ==> "Nuget" |> ignore
    
    // jetbrain dependencies
    "RestoreTools" ==> "InspectCode" |> ignore
    "RestoreTools" ==> "Build" ==> "CleanupCode" |> ignore

    //benchmarks
    "GenerateBench" ==> "NBench" |> ignore 
    
    // all
    "BuildRelease" ==> "All" |> ignore
    "RunTests" ==> "All" |> ignore
    "NBench" ==> "All" |> ignore
    "Nuget" ==> "All" |> ignore
    
    //docs
    "RestoreTools"  ==> "DocFx" |> ignore
    "DocFx" ==> "ServeDocFx" |> ignore
    
    //workaround for https://github.com/fsprojects/FAKE/issues/2744
    Microsoft.Build.Logging.StructuredLogger.Strings.Initialize()
    
    "Help" // <- default target

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

System.Environment.GetCommandLineArgs()
|> Array.skip 2
|> Array.toList
|> Context.FakeExecutionContext.Create false "build.fsx"
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext
|> initTargets 
|> Target.runOrDefaultWithArguments

0