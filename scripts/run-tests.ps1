Write-Host "Running Tests..." -ForegroundColor green

Get-ChildItem ".\NUnit.Extension.TestMonitor\NUnit.Extension.TestMonitor.Tests\" -recurse | where {$_.extension -eq ".csproj"} | % { dotnet test --no-build --no-restore --test-adapter-path:. --logger:Appveyor /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura $_.FullName }
