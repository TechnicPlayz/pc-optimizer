
      - name: Build installer
        shell: pwsh
        run: |
          New-Item -ItemType Directory -Force -Path dist |
            Out-Null

          $compiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"

          if (!(Test-Path $compiler)) {
            throw "Inno Setup compiler not found: $compiler"
          }

          & $compiler "/DAppVersion=$env:APP_VERSION" "installer/VeloraPC.iss"

          if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed."
          }

          $installer = "dist/VeloraPC-Setup-$env:APP_VERSION.exe"

          if (!(Test-Path $installer)) {
            throw "Expected installer not found: $installer"
          }

          Write-Host "Installer created: $installer"
