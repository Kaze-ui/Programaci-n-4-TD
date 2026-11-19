---
name: unity-hotbuild-sync
description: >-
  Use this skill when compiling Unity C# scripts from the command line and synchronizing
  the resulting Assembly-CSharp.dll directly into the standalone build executable folder
  and Editor script assemblies without waiting for full editor rebuilds.
---

# Unity Hot-Build & Assembly Sync

This skill allows instant compilation of Unity C# scripts in this repository using the bundled Roslyn compiler, verifying code integrity (0 compilation errors) and updating the standalone playable game executable in seconds.

## When to Use
- After modifying any `.cs` script in `Assets/Scenes/`.
- When verifying compiler errors or obsolete API warnings.
- When the user tests the game via the standalone build in `Build And Run/`.

## Compilation Procedure

### Step 1: Run Roslyn Compilation via PowerShell
Execute the command line below in the project root:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\NetCoreRuntime\dotnet.exe" "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\DotNetSdkRoslyn\csc.dll" "@Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.rsp"
```

- **Exit code 0**: Successful compilation.
- **Exit code 1**: Check the standard output for compile errors (`CSxxxx`).

### Step 2: Synchronize the Compiled Assembly

The compiler outputs the fresh binary to:
`Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.dll`

You **must** copy this file to both destinations:

```powershell
Copy-Item "Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.dll" "Library\ScriptAssemblies\Assembly-CSharp.dll" -Force
Copy-Item "Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.dll" "Build And Run\Proyecto final Ilán Fischer_Data\Managed\Assembly-CSharp.dll" -Force
```

### Step 3: Verify Timestamps

Ensure the standalone build received the fresh build:

```powershell
Get-Item "Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.dll", "Build And Run\Proyecto final Ilán Fischer_Data\Managed\Assembly-CSharp.dll" | Select-Object Name, LastWriteTime
```

