using CommandLine;


namespace gvm_win
{
    [Verb("install", HelpText = "Install [the latest] go version.")]
    public class InstallOptions
    {
        [Option('v', "version", SetName = "remote", Required = true, HelpText = "Version to install.")]
        public string? Version { get; set; }

        [Option('l', "local", SetName = "local", Required = true, HelpText = "Add a local go version to manage by path.")]
        public string? Local { get; set; }
    }

    [Verb("remove", HelpText = "Remove a go version. Deletes all files for gvm-win installed versions.")]
    public class RemoveOptions
    {
        [Option('i', "index", Required = true, HelpText = "Index of installation to remove.")]
        public string? Index { get; set; }
    }

    [Verb("list", HelpText = "List installed go versions.")]
    public class ListOptions
    {
        [Option('r', "remote", Required = false, HelpText = "List versions available on go.dev.")]
        public bool Remote { get; set; }
    }

    [Verb("set", HelpText = "Sets a go version as current.")]
    public class SetOptions
    {
        [Option('i', "index", Required = true, HelpText = "Index of the installation to set.")]
        public string? Index { get; set; }
    }

    [Verb("unset", HelpText = "Unset go installation from environment.")]
    public class UnsetOptions { }

    [Verb("current", HelpText = "Gets the current go version.")]
    public class CurrentOptions { }
}
