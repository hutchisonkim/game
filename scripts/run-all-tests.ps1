# Wrapper to run all tests with special handling for Spark-dependent projects
# - non-Spark test projects: run `dotnet test` on the host
# - Spark test projects: run tests inside the test-runner container (via run-tests-in-container.ps1)

$repoRoot = Resolve-Path "$PSScriptRoot\.."
Set-Location $repoRoot.Path

Write-Host "Discovering test projects..."
$testProjFiles = Get-ChildItem -Path . -Recurse -Include *.csproj | Where-Object { $_.FullName -like "*\tests\*" }

if (-not $testProjFiles) {
    Write-Host "No test projects found."
    exit 0
}

$dotnetExit = 0

foreach ($proj in $testProjFiles) {
    $content = Get-Content $proj.FullName -Raw
    $isSpark = $false
    if ($content -match '<PackageReference\s+Include="Microsoft.Spark"') {
        $isSpark = $true
    }

    Write-Host "`n=== Project: $($proj.FullName) ==="
    if ($isSpark) {
        Write-Host "Detected Microsoft.Spark dependency — running inside container..."
        # run the containerized test script which starts spark and runs the test inside test-runner
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-tests-in-container.ps1')
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Containerized tests failed for $($proj.FullName) with exit code $LASTEXITCODE"
            $dotnetExit = $LASTEXITCODE
            break
        }
    } else {
        Write-Host "Running dotnet test for project on host..."
        dotnet test $proj.FullName -v minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet test failed for $($proj.FullName) with exit code $LASTEXITCODE"
            $dotnetExit = $LASTEXITCODE
            break
        }
    }
}

exit $dotnetExit
