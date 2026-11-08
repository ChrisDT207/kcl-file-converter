# Zoo KCL → STEP Converter (Zoo Studio Edition)

A Windows desktop application designed to batch convert Zoo/KittyCAD `.kcl` models into standard `.step` CAD files using the official Zoo CLI.

---

## Architecture Overview

The codebase is structured into a classic **Three-Tier (3-Layer) Architecture** to ensure clean separation of concerns, high maintainability, and modularity:

```
ZooStepConverter/
├── DataAccess/                         # DATA ACCESS LAYER (DAL)
│   ├── TokenRepository.cs              # DPAPI secure token storage & retrieval
│   ├── ZooApiClient.cs                 # HTTP REST & OAuth device endpoint client
│   └── ZooProcessRunner.cs             # CLI process runner & executable discovery
│
├── BusinessLogic/                      # BUSINESS LOGIC LAYER (BLL)
│   ├── Common/
│   │   ├── ConversionMode.cs           # Enum defining conversion modes (All, Assembly, Parts)
│   │   └── TextHelpers.cs              # JSON parsing, error extractors & text formatters
│   └── Services/
│       ├── AuthService.cs              # OAuth2 device login flow & account preflight checks
│       ├── CliUpdateService.cs         # CLI binary updater with file-lock bypass & fallback
│       └── StepConversionService.cs    # Batch KCL conversion & KCL 3.0 auto-fallback patch
│
├── Presentation/                       # PRESENTATION LAYER (UI)
│   ├── Forms/
│   │   └── ConverterForm.cs            # Windows Forms GUI, controls & user events
│   └── Program.cs                      # App entry point, TLS 1.2+ setup & visual styles
│
├── Build.bat                           # Zero-dependency build script (uses Windows csc.exe)
├── ZooStepConverter.csproj             # Standard Visual Studio / MSBuild project definition
├── Update-ZooCLI.bat                   # Standalone CLI updater batch script
└── Update-ZooCLI.ps1                   # Standalone CLI updater PowerShell script
```

---

## Three-Tier Software Layers

### 1. Data Access Layer (`ZooStepConverter.DataAccess`)
- **`TokenRepository`**: Encapsulates Windows DPAPI (`ProtectedData`) encryption to safely persist and retrieve the user's `token.dat` under `%AppData%\ZooStepConverter`.
- **`ZooApiClient`**: Manages HTTP web requests (`WebRequest`) for OAuth2 device authentication against `https://api.zoo.dev` and GitHub release metadata endpoints.
- **`ZooProcessRunner`**: Discovers the local `zoo.exe` binary and executes CLI processes with standard I/O redirection, asynchronous stream reading, and environment variable injection (`ZOO_API_TOKEN`).

### 2. Business Logic Layer (`ZooStepConverter.BusinessLogic`)
- **`StepConversionService`**: Orchestrates folder scans, file filtering (Assemblies vs. Parts), invocation of `kcl export`, detection and dynamic patching of KCL 3.0 syntax incompatibilities (`kclVersion = "3.0-preview"`), output file relocation, and progress tracking.
- **`AuthService`**: Coordinates OAuth2 device authorization polling, user profile retrieval, account balance / credit limit verification, and status monitoring.
- **`CliUpdateService`**: Manages Zoo CLI version checks, process shutdown, binary download, and atomic file replacement to bypass Windows OS file locks (Error 5).
- **`Common` (`ConversionMode`, `TextHelpers`)**: Domain models, regex-based JSON extraction, error string sanitization, and truncation helpers.

### 3. Presentation Layer (`ZooStepConverter.Presentation`)
- **`ConverterForm`**: Windows Forms UI featuring folder selection, drag-and-drop support, log output window, real-time progress indicators, conversion mode switches, and action buttons.
- **`Program`**: Sets process security protocols (enforces TLS 1.2+ for HTTPS), enables modern visual styles, parses command-line folder arguments, and launches the application form.

---

## Building the Application

### Option 1: Double-click `Build.bat`
Run `ZooStepConverter\Build.bat`. It automatically invokes the C# compiler (`csc.exe`) built into Windows:
```cmd
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /r:System.Security.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:ZooStepConverter.exe /recurse:*.cs
```
*No external SDK or Visual Studio installation is required.*

### Option 2: Visual Studio or MSBuild
Open `ZooStepConverter\ZooStepConverter.csproj` in Visual Studio or run:
```cmd
msbuild ZooStepConverter.csproj /p:Configuration=Release
```

---

## Prerequisites
- Windows 10 / 11
- [.NET Framework 4.5+](https://dotnet.microsoft.com/) (preinstalled on modern Windows)
- [Zoo CLI](https://zoo.dev/docs/developer-tools/cli) installed (managed automatically via the **Update CLI** button)
