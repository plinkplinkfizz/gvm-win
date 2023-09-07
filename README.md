
# gvm-win

The Go Version Manager for Windows




## Installation

There is nothing much to install. The program is largely portable. 


Requires .NET 6.

The program generates the configuration file aptly named `.gvm-win` in the `$USERPROFILE` directory. It is a simple JSON file. If you want to make it completely portable you may choose to move that file into the program directory. The program will search for the configuration file first in its own directory and then in the `$USERPROFILE`. It will take the first file it encounters.

You might want to change `dataDirectory`. It defaults to the `$USERPROFILE\gvm-win-data`.
## Usage/Examples

### Install a Go Version
#### From Remote
```powershell
gvm-win.exe install --version <version-number>
```
The `<version-number>` can be obtained from `gvm-win.exe list --remote`.
#### An Existing Version on the System
```powershell
gvm-win.exe install --local <go-top-level-directory>
```

### Remove a Go Version
```powershell
gvm-win.exe remove --index <index>
```
The `<index>` can be obtained from `gvm-win.exe list`. Files of a local installation will not be deleted. Workspaces of local as well as remote installations will be deleted regardless.

### List Go Versions
#### From go.dev
```powershell
gvm-win.exe list --remote
```
#### Local installations
```powershell
gvm-win.exe list
```

### Set a Go Version
```powershell
gvm-win.exe set --index <index>
```
The `<index>` can be obtained from `gvm-win.exe list`. The User Environment will be populated with Go variables. Running on an elevated console will affect the System Environment.

### Unset a Go Version
```powershell
gvm-win.exe unset
```
This will remove all existing Go variables from the User Environment. As with set, an elevated console will affect the System Environment.

### View the Current Version
```powershell
gvm-win.exe Current
```
## Authors

- [@plinkplinkfizz](https://www.github.com/plinkplinkfizz)


## License

[GNU General Public License v3.0](https://choosealicense.com/licenses/gpl-3.0/)


## Acknowledgements

 - [commandline](https://github.com/commandlineparser/commandline)
 - [Downloader](https://github.com/bezzad/Downloader)
 - [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)


![Logo](gvm-win_250x250.png)

