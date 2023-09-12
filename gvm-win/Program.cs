using CommandLine;
using Downloader;
using System.IO.Compression;
using System.Reflection;

namespace gvm_win
{
    internal class Program
    {
        private const string GoBaseUrl = "https://go.dev/dl/";
        private const string GoVersionsAllUrl = "https://go.dev/dl/?mode=json&include=all";

        static void Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<InstallOptions, RemoveOptions, ListOptions, SetOptions, UnsetOptions, CurrentOptions>(args);
            parserResult.WithParsed<InstallOptions>(o => RunInstall(o));
            parserResult.WithParsed<RemoveOptions>(o => RunRemove(o));
            parserResult.WithParsed<ListOptions>(o => RunList(o));
            parserResult.WithParsed<SetOptions>(o => RunSet(o));
            parserResult.WithParsed<UnsetOptions>(_ => RunUnset());
            parserResult.WithParsed<CurrentOptions>(_ => RunCurrent());
        }

        private static void RunInstall(InstallOptions installOptions)
        {
            string uniqueId = Guid.NewGuid().ToString("N");

            if (installOptions.Version != null)
            {
                foreach (GoInstallation goInstallation in GvmConfig.installations)
                {
                    if (goInstallation.Version != installOptions.Version || goInstallation.Local) continue;
                    Console.WriteLine("This version is already installed! You may remove and install it again.");
                    return;
                }
                
                List<GoVersion>? goVersions;
                try { goVersions = CustomUtils.GetJsonData<List<GoVersion>>(GoVersionsAllUrl); }
                catch (Exception ex) { Console.WriteLine(ex.Message); return; }
                if (goVersions == null)
                {
                    Console.WriteLine("There was an error parsing the data from the server");
                    return;
                }

                GoVersion? installVersion = null;
                foreach (GoVersion goVersion in goVersions)
                {
                    if (goVersion.version.Substring(2) == installOptions.Version)
                    {
                        installVersion = goVersion;
                        Console.WriteLine($"Version match found for {installOptions.Version}...");
                        break;
                    }
                }
                if (installVersion == null)
                {
                    Console.WriteLine("Unable to match version! Please check if your input matches a version as shown in `gvm-win list -r`.");
                    return;
                }

                GoVersionFile? installFile = null;
                foreach (GoVersionFile goVersionFile in installVersion.files)
                {
                    if (goVersionFile is { os: "windows", arch: "amd64", kind: "archive" })
                    {
                        Console.WriteLine("Found a download candidate for Windows amd64...");
                        installFile = goVersionFile;
                        break;
                    }
                }
                if (installFile == null)
                {
                    Console.WriteLine("File for this architecture not available from server!");
                    return;
                }

                string installDirectory = Path.Combine(GvmConfig.dataDirectory, uniqueId);
                if (!Directory.Exists(installDirectory))
                {
                    try { Directory.CreateDirectory(installDirectory); }
                    catch { Console.WriteLine("Unable to create directory in the configured data directory!"); return; }
                }
                DownloadConfiguration downloadConfiguration = new DownloadConfiguration
                {
                    ChunkCount = 50,
                    ParallelCount = 8,
                    ParallelDownload = true,
                    RequestConfiguration = { UserAgent = $"Mozilla/5.0 (Windows; U; Windows NT 10.4; x64; en-US) gvm-win/{Assembly.GetEntryAssembly()?.GetName().Version}", },
                };
                DownloadService downloadService = new DownloadService(downloadConfiguration);

                Console.WriteLine($"Downloading file: {GoBaseUrl}{installFile.filename}");
                downloadService.DownloadFileTaskAsync($"{GoBaseUrl}{installFile.filename}", Path.Combine(installDirectory, installFile.filename)).Wait();
                switch (downloadService.Status)
                {
                    case DownloadStatus.Failed: Console.WriteLine("Download failed! Try again maybe!"); return;
                    case DownloadStatus.Completed: Console.WriteLine("Download completed..."); break;
                    default: Console.WriteLine("Download status in limbo!"); return;
                }
                if (CustomUtils.GetChecksum(Path.Combine(installDirectory, installFile.filename), HashingAlgoTypes.Sha256)?.ToLower() != installFile.sha256.ToLower())
                {
                    Console.WriteLine("Checksum verification failed!");
                    return;
                }

                try { ZipFile.ExtractToDirectory(Path.Combine(installDirectory, installFile.filename), installDirectory); }
                catch { Console.WriteLine("Unable to extract archive into custom directory!"); return; }
                GoInstallation newGoInstallation = new GoInstallation
                {
                    Id = uniqueId,
                    Version = installVersion.version.Substring(2),
                    Stable = installVersion.stable,
                    Local = false,
                    Path = Path.Combine(installDirectory, "go"),
                };
                GvmConfig.installations.Add(newGoInstallation);
                GvmConfig.Save();
                Console.WriteLine("A new go installation has been added to the manager! You can use it by using `gvm-win set -i <index>`.");
            }
            else if (installOptions.Local != null)
            {
                string fullPath = Path.GetFullPath(installOptions.Local);
                foreach (GoInstallation goInstallation in GvmConfig.installations)
                {
                    if (goInstallation.Path == fullPath)
                    {
                        Console.WriteLine("This installation is already managed by gvm-win!");
                        return;
                    }
                }
                try
                {
                    List<string> output = CustomUtils.RunCommand(Path.Combine(installOptions.Local, "bin\\go.exe"), "version").Split(' ').ToList();
                    if (output[0] == "go" && output[1] == "version")
                    {
                        GoInstallation goInstallation = new GoInstallation
                        {
                            Id = uniqueId,
                            Local = true,
                            Version = output[2].Substring(2),
                            Path = fullPath,
                        };
                        try
                        {
                            List<GoVersion>? goVersions = CustomUtils.GetJsonData<List<GoVersion>>(GoVersionsAllUrl);
                            if (goVersions != null)
                            {
                                foreach (GoVersion goVersion in goVersions)
                                {
                                    if (goVersion.version.Substring(2) == goInstallation.Version)
                                    {
                                        Console.WriteLine("Version matched with server!");
                                        goInstallation.Stable = goVersion.stable;
                                        break;
                                    }
                                }
                            }
                            else { Console.WriteLine("There was an error getting data from server! Setting stability to unstable!"); }
                        }
                        catch (Exception ex) { Console.WriteLine($"{ex.Message} Setting stability to unstable!"); }
                        GvmConfig.installations.Add(goInstallation);
                        GvmConfig.Save();
                        Console.WriteLine($"Go version {goInstallation.Version} added successfully from {fullPath}");
                    }
                }
                catch { Console.WriteLine("Unable to find a go executable at the current location!"); }
            }
        }

        private static void RunRemove(RemoveOptions removeOptions)
        {
            if (removeOptions.Index == null) return;

            if (!int.TryParse(removeOptions.Index, out var index)) { Console.WriteLine("Unable to parse input version! Please use an index listed in `gvm-win list`."); return; }

            if (index < 0 || index >= GvmConfig.installations.Count)
            {
                Console.WriteLine("The index you have mentioned does not exist! Please use an index listed in `gvm-win list`.");
                return;
            }
            Console.WriteLine("Are you sure that you want to remove this installation? (y / N): ");
            string? response = Console.ReadLine();
            if (response == null || response.ToLower() != "y") return;

            Console.WriteLine($"Deleting installation at {GvmConfig.installations[index].Path}");
            try 
            {
                string deletePath = Path.Combine(GvmConfig.dataDirectory, GvmConfig.installations[index].Id);
                DirectoryInfo directoryInfo = new DirectoryInfo(deletePath) { Attributes = FileAttributes.Normal };
                foreach (FileSystemInfo fileSystemInfo in directoryInfo.GetFileSystemInfos("*", SearchOption.AllDirectories)) 
                {
                    fileSystemInfo.Attributes = FileAttributes.Normal;
                }
                directoryInfo.Delete(true);
            }
            catch (DirectoryNotFoundException) { Console.WriteLine("Directory not found! Removing entries from database anyway!"); }
            catch { Console.WriteLine("Unable to remove installation!"); return; }
            if (GvmConfig.installations[index].Id == GvmConfig.current) { RunUnset(); }
            GvmConfig.installations.RemoveAt(index);
            GvmConfig.Save();
        }

        private static void RunList(ListOptions listOptions)
        {
            if (listOptions.Remote)
            {
                List<GoVersion>? goVersions = null;
                try { goVersions = CustomUtils.GetJsonData<List<GoVersion>>(GoVersionsAllUrl); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }

                if (goVersions != null)
                {
                    foreach (GoVersion? goVersion in goVersions)
                    {
                        if (goVersion.stable)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"{goVersion.version.Substring(2)} (stable)");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{goVersion.version.Substring(2)} (unstable)");
                        }
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                if (GvmConfig.installations.Count == 0)
                {
                    Console.WriteLine("No Go installations found!");
                    return;
                }
                int i = 0;
                foreach (GoInstallation goInstallation in GvmConfig.installations)
                {
                    Console.WriteLine($"[{i}] => {goInstallation.Version} ({(goInstallation.Local ? "Local, " : "")}{(goInstallation.Stable ? "Stable" : "Unstable")}) @ {goInstallation.Path}");
                    i++;
                }
            }
        }

        private static void RunSet(SetOptions setOptions)
        {
            if (!int.TryParse(setOptions.Index, out var index) ) { Console.WriteLine("Failed to parse the input!"); return; }
            
            if (index < 0 || index >= GvmConfig.installations.Count)
            {
                Console.WriteLine("The index you have mentioned does not exist! Please use an index listed in `gvm-win list`.");
                return;
            }
            EnvironmentVariableTarget variableTarget = EnvironmentVariableTarget.User;
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                variableTarget = EnvironmentVariableTarget.Machine;
            }
            List<string>? paths;
            try { paths = Environment.GetEnvironmentVariable("Path", variableTarget)?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(); }
            catch { Console.WriteLine("Unable to enumerate environment! Please try restarting your console."); return; }

            if (paths == null) { Console.WriteLine("Unable to access environment!"); return; }
            for (int i = paths.Count - 1; i >= 0; i--) { if (GvmConfig.currentBinPath == paths[i]) { paths.RemoveAt(i); } }

            GvmConfig.current = GvmConfig.installations[index].Id;
            GvmConfig.currentBinPath = Path.Combine(GvmConfig.installations[index].Path, "bin");
            paths.Add(GvmConfig.currentBinPath);

            string goPath = Path.Combine(GvmConfig.dataDirectory, GvmConfig.installations[index].Id, "workspace");
            if (!Directory.Exists(goPath))
            {
                try { Directory.CreateDirectory(goPath); }
                catch { Console.WriteLine("Unable to create workspace directory!"); return; }
            }

            Environment.SetEnvironmentVariable("Path", string.Join(';', paths), variableTarget);
            Environment.SetEnvironmentVariable("GOROOT", GvmConfig.installations[index].Path, variableTarget);
            Environment.SetEnvironmentVariable("GOPATH", goPath, variableTarget);

            GvmConfig.Save();

            Console.WriteLine($"The system is now set to use go version {GvmConfig.installations[index].Version}!");

        }

        private static void RunUnset() 
        {
            EnvironmentVariableTarget variableTarget = EnvironmentVariableTarget.User;
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                variableTarget = EnvironmentVariableTarget.Machine;
            }
            
            List<string>? paths;
            try { paths = Environment.GetEnvironmentVariable("Path", variableTarget)?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(); }
            catch { Console.WriteLine("Unable to enumerate environment! Please try restarting your console."); return; }
            
            if (paths == null) { Console.WriteLine("Unable to access environment!"); return; }
            for (int i = paths.Count - 1; i >= 0; i--) { if (GvmConfig.currentBinPath == paths[i]) { paths.RemoveAt(i); } }

            GvmConfig.current = string.Empty;
            GvmConfig.currentBinPath = string.Empty;

            Environment.SetEnvironmentVariable("Path", string.Join(';', paths), variableTarget);
            Environment.SetEnvironmentVariable("GOROOT", null, variableTarget);
            Environment.SetEnvironmentVariable("GOPATH", null, variableTarget);

            GvmConfig.Save();

            Console.WriteLine("All variables pertaining to Go have been removed from the environment.");
        }

        private static void RunCurrent()
        {
            if(string.IsNullOrEmpty(GvmConfig.current)) { Console.WriteLine("No go version set as default!"); return; }
            foreach(GoInstallation goInstallation in GvmConfig.installations) 
            {
                if(GvmConfig.current == goInstallation.Id) 
                {
                    Console.WriteLine($"{goInstallation.Id} => {goInstallation.Version} ({(goInstallation.Local ? "Local, " : "")}{(goInstallation.Stable ? "Stable" : "Unstable")}) @ {goInstallation.Path}");
                    return;
                }
            }
            Console.WriteLine("There seems to be some misconfiguration in the save file!");
        }
    }
}