///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target          = Argument("target", "Default");
var configuration   = Argument("configuration", "Release");
var branchName      = "master";

Information("Branch is '{0}'", branchName);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var isLocalBuild        = !AppVeyor.IsRunningOnAppVeyor;
var isPullRequest       = AppVeyor.Environment.PullRequest.IsPullRequest;
var solutions           = GetFiles("./**/*.sln");
var solutionDirs        = solutions.Select(solution => solution.GetDirectory());
var releaseNotes        = "";
var semVersion = "0.0.2.002";
var version             = semVersion;
var binDir              = "./src/Cake.ProjHelpers/Cake.ProjHelpers/bin/" + configuration;
var nugetRoot           = "./nuget/";
var isMasterBranch      = branchName == "master";

var assemblyInfo        = new AssemblyInfoSettings {
                                Title                   = "Cake.ProjHelpers",
                                Description             = "Cake AddIn to embed files into .proj files",
                                Product                 = "Cake.ProjHelpers",
                                Company                 = "Ori Almog",
                                Version                 = version,
                                FileVersion             = version,
                                InformationalVersion    = semVersion,
                                Copyright               = string.Format("Copyright © Ori Almog {0}", DateTime.Now.Year),
                                CLSCompliant            = true
                            };
var nuspecFiles = new [] 
{
    new NuSpecContent {Source = "Cake.ProjHelpers.dll"},
    new NuSpecContent {Source = "Cake.ProjHelpers.xml"}
};
var nuGetPackSettings   = new NuGetPackSettings {
                                Id                      = assemblyInfo.Product,
                                Version                 = assemblyInfo.InformationalVersion,
                                Title                   = assemblyInfo.Title,
                                Authors                 = new[] {assemblyInfo.Company},
                                Owners                  = new[] {assemblyInfo.Company},
                                Description             = assemblyInfo.Description,
                                Summary                 = "Cake AddIn to embed files into .proj files", 
                                ProjectUrl              = new Uri("https://github.com/orialmog/Cake.ProjHelpers"),
                                IconUrl                 = new Uri("https://raw.githubusercontent.com/cake-build/graphics/master/png/cake-medium.png"),
                                LicenseUrl              = new Uri("https://github.com/orialmog/Cake.ProjHelpers/blob/master/LICENSE"),
                                Copyright               = assemblyInfo.Copyright,
                                Tags                    = new [] {"Cake", "Script", "Build", "Resources", "Embed", "Task"},
                                RequireLicenseAcceptance= false,        
                                Symbols                 = false,
                                NoPackageAnalysis       = true,
                                Files                   = nuspecFiles,
                                BasePath                = binDir, 
                                OutputDirectory         = nugetRoot
                            };

///////////////////////////////////////////////////////////////////////////////
// Output some information about the current build.
///////////////////////////////////////////////////////////////////////////////
var buildStartMessage = string.Format("Building version {0} of {1} ({2}).", version, assemblyInfo.Product, semVersion);
Information(buildStartMessage);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
});

Teardown(() =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    
    Information("Cleaning {0}", nugetRoot);
    CleanDirectories(new DirectoryPath(nugetRoot).FullPath); 
       
    // Clean solution directories.
    foreach(var solutionDir in solutionDirs)
    {
        Information("Cleaning {0}", solutionDir);

        CleanDirectories(solutionDir + "/**/bin/" + configuration);
        CleanDirectories(solutionDir + "/**/obj/" + configuration);
    }
});

Task("Restore")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}", solution);
        NuGetRestore(solution);
    }
});

Task("AssemblyInfo")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var file = "./src/Cake.ProjHelpers/Cake.ProjHelpers/Properties/AssemblyInfo.cs";
    CreateAssemblyInfo(file, assemblyInfo);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("AssemblyInfo")
    .Does(() =>
{
    // Build all solutions.
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);
        MSBuild(solution, settings => 
            settings.SetConfiguration(configuration));
    }
});

Task("Create-NuGet-Packages")
    .IsDependentOn("Build")
    .Does(() =>
{
    if (!System.IO.Directory.Exists(nugetRoot))
    {
        CreateDirectory(nugetRoot);
    }
    NuGetPack("./nuspec/Cake.ProjHelpers.nuspec", nuGetPackSettings);
}); 

 



Task("Publish-NuGet-Packages")
    .IsDependentOn("Create-NuGet-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest) 
    .Does(() =>
{
    var packages  = GetFiles("./nuget/*.nupkg");
    foreach (var package in packages)
    {
        Information(string.Format("Found {0}", package));

        // Push the package.
        string apiKey = EnvironmentVariable("NUGET_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("NUGET_API_KEY variable not found");
        }

        NuGetPush(package, new NuGetPushSettings {
                Source = "https://www.nuget.org/api/v2/package",
                ApiKey = apiKey
            }); 
    }
}); 

Task("Default")
    .IsDependentOn("Create-NuGet-Packages");

Task("AppVeyor")
    .IsDependentOn("Publish-NuGet-Packages");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);

 