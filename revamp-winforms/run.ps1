Write-Host "Building EazyRent ..."
dotnet build
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit $LASTEXITCODE }

Write-Host "Running..."
dotnet run --project .\EazyRentRevamp.csproj
