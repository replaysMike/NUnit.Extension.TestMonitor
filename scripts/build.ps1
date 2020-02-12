Write-Host "Building..." -ForegroundColor green

dotnet build .\NUnit.Extension.TestMonitor\NUnit.Extension.TestMonitor.sln | Select-String -Pattern " warning " -NotMatch | Select-String -Pattern "INFO:" -NotMatch | Select-String -Pattern "WARNING:" -NotMatch

Write-Host "Packaging..." -ForegroundColor green
dotnet pack .\NUnit.Extension.TestMonitor\NUnit.Extension.TestMonitor\NUnit.Extension.TestMonitor.csproj --configuration Release --no-build

Get-ChildItem .\*.nupkg -recurse | % { Push-AppveyorArtifact $_.FullName -FileName $_.Name }
