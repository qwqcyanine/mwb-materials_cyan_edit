# MWB Mats

### Tool designed to mimic PBR with Source Engine's shader system.

![laugh](https://raw.githubusercontent.com/9lbw/mwb-materials/refs/heads/main/autoconverters.png)

If you need help click ![here](https://github.com/mushroom-guy/mwb-materials/blob/main/help.md).

## Note for me

Debug build:

```powershell
dotnet msbuild .\mwb-materials\mwb-materials.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:GenerateResourceMSBuildRuntime=CurrentRuntime
```

Release build:

```powershell
dotnet msbuild .\mwb-materials\mwb-materials.sln /p:Configuration=Release /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:GenerateResourceMSBuildRuntime=CurrentRuntime
```
