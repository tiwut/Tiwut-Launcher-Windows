# Tiwut Launcher (Windows Edition)

A premium, frosted Acrylic/Mica native Windows launcher designed exclusively for installing, compiling, running, and managing Tiwut applications in user-space environment. Built with a 100% native C#/.NET 8 backend integrated with a responsive Microsoft Edge WebView2 user interface.

<img width="1074" height="855" alt="image" src="https://github.com/user-attachments/assets/ccd39d64-a4c0-4188-b565-fe2ea51e4d7b" />

---

## Features

- **Frosted Glass Modern UI**: Translucent window overlay with smooth hover effects, micro-animations, full CSS glassmorphism, dynamic console logs, and built-in tabbed navigation. Supports native Windows 11 Acrylic backdrops and Immersive Dark Mode.
- **Asynchronous Downloader with Fail-safes**: Downloads remote files asynchronously and safely. Reports progress, unzips packages natively into the user's `%AppData%\TiwutLauncher\apps` directory, scans for binary executables, and handles magic bytes validation (magic header `PK` detection) to extract ZIPs incorrectly renamed as raw executables.
- **Asynchronous Compiler State Machine**: Clones repositories from GitHub (or fetches branch ZIP files if git cloning fails) and automatically compiles them using `build.cmd` present in the repository root. Streams compiler logs (stdout/stderr) live directly into the launcher's web console.
- **Detached Launch & Graceful Uninstaller**: Launches applications fully detached from the main launcher process, terminates running instances gracefully (by binary name and repo tags), and completely purges local files and binary caches.

---

## Architecture & Tech Stack

Tiwut Launcher utilizes a dual-engine architecture:

1. **Core Engine (C# / .NET 8 WPF)**: Handles OS interaction, file system scanning, ZIP package extraction, process management, and local script execution.
2. **Frontend UI (HTML5 / Vanilla CSS / JavaScript)**: A modern reactive visual layer rendered inside Microsoft Edge WebView2 with a bridge that triggers system RPC methods.

---

## Building & Running

### Prerequisites

- Windows 10/11 Operating System.
- .NET 8.0 SDK (version 8.0.303 or higher).

### Installation & Build

1. Clone this repository to your local machine:
   ```bash
   git clone https://github.com/tiwut/Tiwut-Launcher-Windows.git
   cd Tiwut-Launcher-Windows
   ```

2. Open and run the build script helper:
   ```cmd
   build.cmd
   ```
   Choose **Option 2** to clean and compile the application.

3. Launch the standalone compiled binary:
   ```cmd
   dist\TiwutLauncher.exe
   ```

---

## Project Directory Structure

```lis
├── TiwutLauncher.csproj   # MSBuild project configuration
├── App.xaml               # Startup application configuration
├── App.xaml.cs            # WPF app entry code
├── MainWindow.xaml        # WPF transparency layout and title controls
├── MainWindow.xaml.cs     # Windows RPC wrapper layer and build/download engines
├── build.cmd              # Command helper script to clean and publish the project
├── src/
│   └── index.html         # Rich Liquid-Glass UI & frontend application logic
└── dist/                  # Standalone published binary folder
```

---

## License

This project is open-source and available under the MIT License.
