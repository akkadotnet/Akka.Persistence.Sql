#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.DotNetCli

// Information about the project for Nuget and Assembly info files
let configuration = "Release"

// Read release notes and version
let solutionFile = FindFirstMatchingFile "*.sln" __SOURCE_DIRECTORY__  // dynamically look up the solution
let buildNumber = environVarOrDefault "BUILD_NUMBER" "0"
let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else DateTime.UtcNow.Ticks.ToString())

let releaseNotes =
    File.ReadLines (__SOURCE_DIRECTORY__ @@ "RELEASE_NOTES.md")
    |> ReleaseNotesHelper.parseReleaseNotes

let versionFromReleaseNotes =
    match releaseNotes.SemVer.PreRelease with
    | Some r -> r.Origin
    | None -> ""

let versionSuffix =
    match (getBuildParam "nugetprerelease") with
    | "dev" -> preReleaseVersionSuffix
    | "" -> versionFromReleaseNotes
    | str -> str

// Directories
let toolsDir = __SOURCE_DIRECTORY__ @@ "tools"
let output = __SOURCE_DIRECTORY__  @@ "bin"
let outputTests = __SOURCE_DIRECTORY__ @@ "TestResults"
let outputPerfTests = __SOURCE_DIRECTORY__ @@ "PerfResults"
let outputNuGet = output @@ "nuget"

Target "Clean" (fun _ ->
    ActivateFinalTarget "KillCreatedProcesses"

    CleanDir output
    CleanDir outputTests
    CleanDir outputPerfTests
    CleanDir outputNuGet
)

Target "AssemblyInfo" (fun _ ->
    XmlPokeInnerText "./src/Directory.Generated.props" "//Project/PropertyGroup/VersionPrefix" releaseNotes.AssemblyVersion
    XmlPokeInnerText "./src/Directory.Generated.props" "//Project/PropertyGroup/PackageReleaseNotes" (releaseNotes.Notes |> String.concat "\n")
)

Target "Build" (fun _ ->
    DotNetCli.Build
        (fun p ->
            { p with
                Project = solutionFile
                Configuration = configuration }) // "Rebuild"
)

//--------------------------------------------------------------------------------
// Tests targets
//--------------------------------------------------------------------------------
module internal ResultHandling =
    let (|OK|Failure|) = function
        | 0 -> OK
        | x -> Failure x

    let buildErrorMessage = function
        | OK -> None
        | Failure errorCode ->
            Some (sprintf "xUnit2 reported an error (Error Code %d)" errorCode)

    let failBuildWithMessage = function
        | DontFailBuild -> traceError
        | _ -> (fun m -> raise(FailedTestsException m))

    let failBuildIfXUnitReportedError errorLevel =
        buildErrorMessage
        >> Option.iter (failBuildWithMessage errorLevel)

Target "RunTests" (fun _ ->
    let projects =
        match (isWindows) with
        | true -> !! "./src/**/*.Tests.csproj"
                  -- "./src/**/*.Benchmark.*.csproj"
                  -- "./src/**/*.Data.Compatibility.Tests.csproj" // All of the data docker images are Linux only
        | _ -> !! "./src/**/*.Tests.csproj"
               -- "./src/**/*.Benchmark.*.csproj"
               ++  "./src/**/*.DockerTests.csproj" // if you need to filter specs for Linux vs. Windows, do it here

    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none -teamcity" (outputTests))
            | false -> (sprintf "test -c Release --no-build --logger:trx --logger:\"console;verbosity=normal\" --results-directory %s -- -parallel none" (outputTests))

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0)

        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

    projects |> Seq.iter (log)
    projects |> Seq.iter (runSingleProject)
)

Target "NBench" <| fun _ ->
    let projects =
        match (isWindows) with
        | true -> !! "./src/**/*.Tests.Performance.csproj"
        | _ -> !! "./src/**/*.Tests.Performance.csproj" // if you need to filter specs for Linux vs. Windows, do it here


    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "nbench --nobuild --teamcity --concurrent true --trace true --output %s" (outputPerfTests))
            | false -> (sprintf "nbench --nobuild --concurrent true --trace true --output %s" (outputPerfTests))

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0)

        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

    projects |> Seq.iter runSingleProject

//--------------------------------------------------------------------------------
// Nuget targets
//--------------------------------------------------------------------------------
let overrideVersionSuffix (project:string) =
    match project with
    | _ -> versionSuffix // add additional matches to publish different versions for different projects in solution

Target "CreateNuget" (fun _ ->
    let projects = !! "src/**/*.csproj"
                   -- "src/**/*Tests.csproj" // Don't publish unit tests
                   -- "src/**/*Tests*.csproj"
                   -- "src/**/*.Benchmark.*.csproj"

    let runSingleProject project =
        DotNetCli.Pack
            (fun p ->
                { p with
                    Project = project
                    Configuration = configuration
                    AdditionalArgs = ["--include-symbols --no-build"]
                    VersionSuffix = overrideVersionSuffix project
                    OutputPath = outputNuGet })

    projects |> Seq.iter (runSingleProject)
)

Target "PublishNuget" (fun _ ->
    let projects = !! "./bin/nuget/*.nupkg"
    let apiKey = getBuildParamOrDefault "nugetkey" ""
    let source = getBuildParamOrDefault "nugetpublishurl" ""
    let symbolSource = source
    let shouldPublishSymbolsPackages = not (symbolSource = "")

    if (not (source = "") && not (apiKey = "") && shouldPublishSymbolsPackages) then
        let runSingleProject project =
            DotNetCli.RunCommand
                (fun p ->
                    { p with
                        TimeOut = TimeSpan.FromMinutes 10. })
                (sprintf "nuget push %s --api-key %s --source %s" project apiKey source)

        projects |> Seq.iter (runSingleProject)
)

//--------------------------------------------------------------------------------
// JetBrain targets
//--------------------------------------------------------------------------------
Target "InspectCode" (fun _ ->
    DotNetCli.RunCommand
        (fun p ->
            { p with
                TimeOut = TimeSpan.FromMinutes 10. })
        "tool restore"

    DotNetCli.RunCommand
        (fun p ->
            { p with
                TimeOut = TimeSpan.FromMinutes 10. })
        "dotnet jb inspectcode Akka.Persistence.Sql.sln --build --swea --properties=\"Configuration=Release\" --telemetry-optout --format=\"Html;Xml;Text\" --output=\"TestResults/Akka.Persistence.Sql.jb\""
)

Target "CleanupCode" (fun _ ->
    DotNetCli.RunCommand
        (fun p ->
            { p with
                TimeOut = TimeSpan.FromMinutes 10. })
        "tool restore"

    DotNetCli.RunCommand
        (fun p ->
            { p with
                TimeOut = TimeSpan.FromMinutes 10. })
        "dotnet jb cleanupcode Akka.Persistence.Sql.sln --profile=\"Akka.NET\" --properties=\"Configuration=Release\" --telemetry-optout"
)

//--------------------------------------------------------------------------------
// Cleanup
//--------------------------------------------------------------------------------
FinalTarget "KillCreatedProcesses" (fun _ ->
    log "Shutting down dotnet build-server"
    let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- "build-server shutdown") (System.TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwithf "dotnet build-server shutdown failed"
)

//--------------------------------------------------------------------------------
// Help
//--------------------------------------------------------------------------------
Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "./build.ps1 [target]"
      ""
      " Targets for building:"
      " * Build         Builds"
      " * Nuget         Create and optionally publish nugets packages"
      " * RunTests      Runs tests"
      " * All           Builds, run tests, creates and optionally publish nuget packages"
      ""
      " Other Targets"
      " * Help       Display this help"
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------
Target "BuildRelease" DoNothing
Target "All" DoNothing
Target "Nuget" DoNothing

// build dependencies
"Clean" ==> "AssemblyInfo" ==> "Build" ==> "InspectCode" ==> "BuildRelease"

// tests dependencies
"Build" ==> "RunTests"

// nuget dependencies
"Clean" ==> "Build" ==> "CreateNuget"
"CreateNuget" ==> "PublishNuget" ==> "Nuget"

// jetbrain dependencies
"InspectCode"
"Build" ==> "CleanupCode"

// all
"BuildRelease" ==> "All"
"RunTests" ==> "All"
"NBench" ==> "All"
"Nuget" ==> "All"

RunTargetOrDefault "Help"
