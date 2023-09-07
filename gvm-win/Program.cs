using CommandLine;
using Downloader;
using System;
using System.IO.Compression;
using System.Reflection;

namespace gvm_win
{
    internal class Program
    {
        const string GO_BASE_URL = "https://go.dev/dl/";
        const string GO_VERSIONS_ALL_URL = "https://go.dev/dl/?mode=json&include=all";

        static void Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<InstallOptions, RemoveOptions, ListOptions, SetOptions, UnsetOptions, CurrentOptions>(args);
            parserResult.WithParsed<InstallOptions>(o => RunInstall(o));
            parserResult.WithParsed<RemoveOptions>(o => RunRemove(o));
            parserResult.WithParsed<ListOptions>(o => RunList(o));
            parserResult.WithParsed<SetOptions>(o => RunSet(o));
            parserResult.WithParsed<UnsetOptions>(o => RunUnset(o));
            parserResult.WithParsed<CurrentOptions>(o => RunCurrent(o));
        }

        public static void RunInstall(InstallOptions installOptions)
        {
            string uniqueId = Guid.NewGuid().ToString("N");

            if (installOptions.Version != null)
            {
                foreach (GoInstallation goInstallation in GVMConfig.installations)
                {
                    if (goInstallation.Version == installOptions.Version && !goInstallation.Local)
                    {
                        Console.WriteLine("This version is already installed! You may remove and install it again.");
                        return;
                    }
                }
                
                List<GoVersion>? goVersions = null;
                try { goVersions = CustomUtils.GetJSONData<List<GoVersion>>(GO_VERSIONS_ALL_URL); }
                catch (Exception ex) { Console.WriteLine(ex.Message); return; }
                if (goVersions == null)
                {
                    Console.WriteLine("There was an error parsing the data from the server");
                    return;
                }

                GoVersion? installVersion = null;
                foreach (GoVersion goVersion in goVersions)
                {
                    if (goVersion.version?.Substring(2) == installOptions.Version)
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
                    if (goVersionFile.os == "windows" && goVersionFile.arch == "amd64" && goVersionFile.kind == "archive")
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

                string installDirectory = System.IO.Path.Combine(GVMConfig.dataDirectory, uniqueId);
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
                    RequestConfiguration = { UserAgent = $"Mozilla/5.0 (Windows; U; Windows NT 10.4; x64; en-US) gvm-win/{Assembly.GetEntryAssembly().GetName().Version}", },
                };
                DownloadService downloadService = new DownloadService(downloadConfiguration);

                Console.WriteLine($"Downloading file: {GO_BASE_URL}{installFile.filename}");
                downloadService.DownloadFileTaskAsync($"{GO_BASE_URL}{installFile.filename}", System.IO.Path.Combine(installDirectory, installFile.filename)).Wait();
                switch (downloadService.Status)
                {
                    case DownloadStatus.Failed: Console.WriteLine("Download failed! Try again maybe!"); return;
                    case DownloadStatus.Completed: Console.WriteLine("Download completed..."); break;
                    default: Console.WriteLine("Download status in limbo!"); return;
                }
                if (CustomUtils.GetChecksum(System.IO.Path.Combine(installDirectory, installFile.filename), HashingAlgoTypes.SHA256).ToLower() != installFile.sha256.ToLower())
                {
                    Console.WriteLine("Checksum verification failed!");
                    return;
                }

                try { ZipFile.ExtractToDirectory(System.IO.Path.Combine(installDirectory, installFile.filename), installDirectory); }
                catch { Console.WriteLine("Unable to extract archive into custom directory!"); return; }
                GoInstallation newGoInstallation = new GoInstallation
                {
                    Id = uniqueId,
                    Version = installVersion.version.Substring(2),
                    Stable = installVersion.stable,
                    Local = false,
                    Path = System.IO.Path.Combine(installDirectory, "go"),
                };
                GVMConfig.installations.Add(newGoInstallation);
                GVMConfig.Save();
                Console.WriteLine("A new go installation has been added to the manager! You can use it by using `gvm-win set -i <index>`.");
            }
            else if (installOptions.Local != null)
            {
                string fullPath = System.IO.Path.GetFullPath(installOptions.Local);
                foreach (GoInstallation goInstallation in GVMConfig.installations)
                {
                    if (goInstallation.Path == fullPath)
                    {
                        Console.WriteLine("This installation is already managed by gvm-win!");
                        return;
                    }
                }
                try
                {
                    List<string> output = CustomUtils.RunCommand(System.IO.Path.Combine(installOptions.Local, "bin\\go.exe"), "version").Split(' ').ToList();
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
                            List<GoVersion>? goVersions = CustomUtils.GetJSONData<List<GoVersion>>(GO_VERSIONS_ALL_URL);
                            if (goVersions != null)
                            {
                                foreach (GoVersion goVersion in goVersions)
                                {
                                    if (goVersion.version?.Substring(2) == goInstallation.Version)
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
                        GVMConfig.installations.Add(goInstallation);
                        GVMConfig.Save();
                        Console.WriteLine($"Go version {goInstallation.Version} added successfully from {fullPath}");
                    }
                }
                catch { Console.WriteLine("Unable to find a go executable at the current location!"); }
            }
        }

        public static void RunRemove(RemoveOptions removeOptions)
        {
            if (removeOptions.Index == null) return;

            int index = 0;
            if (int.TryParse(removeOptions.Index, out index)) { Console.WriteLine("Unable to parse input version! Please use an index listed in `gvm-win list`."); return; }
            
            if (index < 0 || index >= GVMConfig.installations.Count)
            {
                Console.WriteLine("The index you have mentioned does not exist! Please use an index listed in `gvm-win list`.");
                return;
            }
            Console.WriteLine("Are you sure that you want to remove this installation? (y / N): ");
            string? response = Console.ReadLine();
            if (response == null || response.ToLower() != "y") return;

            Console.WriteLine($"Deleting installation at {GVMConfig.installations[index].Path}");
            try { Directory.Delete(System.IO.Path.Combine(GVMConfig.dataDirectory, GVMConfig.installations[index].Id), true); }
            catch (DirectoryNotFoundException) { Console.WriteLine("Directory not found! Removing entries from database anyway!"); }
            catch { Console.WriteLine("Unable to remove installation!"); return; }
            GVMConfig.installations.RemoveAt(index);
            GVMConfig.Save();
        }

        public static void RunList(ListOptions listOptions)
        {
            if (listOptions.Remote)
            {
                List<GoVersion>? goVersions = null;
                try { goVersions = CustomUtils.GetJSONData<List<GoVersion>>(GO_VERSIONS_ALL_URL); }
                catch (Exception ex) { Console.WriteLine(ex.Message); }

                if (goVersions != null)
                {
                    foreach (GoVersion? goVersion in goVersions)
                    {
                        if (goVersion.stable)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"{goVersion.version?.Substring(2)} (stable)");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{goVersion.version?.Substring(2)} (unstable)");
                        }
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                if (GVMConfig.installations.Count == 0)
                {
                    Console.WriteLine("No Go installations found!");
                    return;
                }
                int i = 0;
                foreach (GoInstallation goInstallation in GVMConfig.installations)
                {
                    Console.WriteLine($"[{i}] => {goInstallation.Version} ({(goInstallation.Local ? "Local, " : "")}{(goInstallation.Stable ? "Stable" : "Unstable")}) @ {goInstallation.Path}");
                    i++;
                }
            }
        }

        public static void RunSet(SetOptions setOptions)
        {
            int index = 0;
            if (!int.TryParse(setOptions.Index, out index) ) { Console.WriteLine("Failed to parse the input!"); return; }
            
            if (index < 0 || index >= GVMConfig.installations.Count)
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
            List<string> paths = null;
            try { paths = Environment.GetEnvironmentVariable("Path", variableTarget).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(); }
            catch { Console.WriteLine("Unable to enumerate environment! Please try restarting your console."); return; }

            for (int i = paths.Count - 1; i >= 0; i--) { if (GVMConfig.currentBinPath == paths[i]) { paths.RemoveAt(i); } }

            GVMConfig.current = GVMConfig.installations[index].Id;
            GVMConfig.currentBinPath = System.IO.Path.Combine(GVMConfig.installations[index].Path, "bin");
            paths.Add(GVMConfig.currentBinPath);

            string goPath = System.IO.Path.Combine(GVMConfig.dataDirectory, GVMConfig.installations[index].Id, "workspace");
            if (!Directory.Exists(goPath))
            {
                try { Directory.CreateDirectory(goPath); }
                catch { Console.WriteLine("Unable to create workspace directory!"); return; }
            }

            Environment.SetEnvironmentVariable("Path", string.Join(';', paths), variableTarget);
            Environment.SetEnvironmentVariable("GOROOT", GVMConfig.installations[index].Path, variableTarget);
            Environment.SetEnvironmentVariable("GOPATH", goPath, variableTarget);

            GVMConfig.Save();

            Console.WriteLine($"The system is now set to use go version {GVMConfig.installations[index].Version}!");

        }

        public static void RunUnset(UnsetOptions unsetOptions) 
        {
            EnvironmentVariableTarget variableTarget = EnvironmentVariableTarget.User;
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                variableTarget = EnvironmentVariableTarget.Machine;
            }
            
            List<string> paths = null;
            try { paths = Environment.GetEnvironmentVariable("Path", variableTarget).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(); }
            catch { Console.WriteLine("Unable to enumerate environment! Please try restarting your console."); return; }

            for (int i = paths.Count - 1; i >= 0; i--) { if (GVMConfig.currentBinPath == paths[i]) { paths.RemoveAt(i); } }

            GVMConfig.current = null;
            GVMConfig.currentBinPath = null;

            Environment.SetEnvironmentVariable("Path", string.Join(';', paths), variableTarget);
            Environment.SetEnvironmentVariable("GOROOT", null, variableTarget);
            Environment.SetEnvironmentVariable("GOPATH", null, variableTarget);

            GVMConfig.Save();

            Console.WriteLine("All variables pertaining to Go have been removed from the environment.");
        }

        public static void RunCurrent(CurrentOptions currentOptions)
        {
            if(GVMConfig.current == null || GVMConfig.current == string.Empty) { Console.WriteLine("No go version set as default!"); return; }
            foreach(GoInstallation goInstallation in GVMConfig.installations) 
            {
                if(GVMConfig.current == goInstallation.Id) 
                {
                    Console.WriteLine($"{goInstallation.Id} => {goInstallation.Version} ({(goInstallation.Local ? "Local, " : "")}{(goInstallation.Stable ? "Stable" : "Unstable")}) @ {goInstallation.Path}");
                    return;
                }
            }
            Console.WriteLine("There seems to be some misconfiguration in the save file!");
        }
    }
}