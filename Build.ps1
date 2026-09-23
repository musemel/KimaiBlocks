$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot/KimaiBlocks.csproj" --configuration Release --output "$PSScriptRoot/dist" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath "$PSScriptRoot/LICENSE", "$PSScriptRoot/THIRD_PARTY.md", "$PSScriptRoot/README.md" -Destination "$PSScriptRoot/dist"
Copy-Item -LiteralPath "$PSScriptRoot/docs" -Destination "$PSScriptRoot/dist" -Recurse -Force
Write-Output "Launch: $PSScriptRoot/dist/KimaiBlocks.exe"
