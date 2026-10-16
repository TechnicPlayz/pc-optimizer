
      - name: Restore dependencies
        shell: pwsh
        run: |
          dotnet restore src/VeloraPC/VeloraPC.csproj `
            --runtime win-x64 `
            -p:SelfContained=true

          if ($LASTEXITCODE -ne 0) {
            throw "NuGet restore failed."
          }

      - name: Build Release
        shell: pwsh
        run: |
          dotnet build src/VeloraPC/VeloraPC.csproj `
            --configuration Release `
            --runtime win-x64 `
            --no-restore `
            -p:SelfContained=true `
            "-p:Version=$env:APP_VERSION"

          if ($LASTEXITCODE -ne 0) {
            throw "Release build failed."
          }

      - name: Publish Windows x64 EXE
        shell: pwsh
        run: |
          if (Test-Path publish) {
            Remove-Item publish -Recurse -Force
          }

          dotnet publish src/VeloraPC/VeloraPC.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            --no-restore `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            "-p:Version=$env:APP_VERSION" `
            --output publish

          if ($LASTEXITCODE -ne 0) {
            throw "Release publish failed."
          }

          if (!(Test-Path "publish/VeloraPC.exe")) {
            Get-ChildItem publish -Recurse |
              Select-Object FullName

            throw "VeloraPC.exe was not created."
          }
