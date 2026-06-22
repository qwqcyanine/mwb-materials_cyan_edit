# MWB Mats

### Tool designed to mimic PBR with Source Engine's shader system.

![robot](https://media.discordapp.net/attachments/839227966193795093/1138863562933162065/image.png?width=1439&height=509)

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
