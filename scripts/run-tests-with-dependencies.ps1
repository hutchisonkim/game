#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs xUnit tests with dependency-aware execution based on TestDependencyGraph.json
    
.DESCRIPTION
    This script reads TestDependencyGraph.json to understand test dependencies and runs
    tests in the correct order. If a parent test fails, dependent tests are skipped.
    
.PARAMETER Filter
    Run tests matching specific criteria:
    - "Essential=true" : Run all essential tests
    - "Category=Foundation" : Run all tests in a category
    - "ChessRule=A1" : Run tests for a specific chess rule
    - "DependsOn=L0_Foundation_BoardStateProvider" : Run tests dependent on a test
    - "Layer=0" : Run tests for a specific layer
    
.PARAMETER MaxDepth
    For analysis only: show dependency depth to this level (default: 5)
    
.PARAMETER Analyze
    Analyze dependencies without running tests
    
.PARAMETER ShowDetails
    Show detailed output including test execution order and skip reasons
    
.EXAMPLE
    ./run-tests-with-dependencies.ps1 -Filter "Essential=true"
    ./run-tests-with-dependencies.ps1 -Filter "Category=Foundation" -ShowDetails
    ./run-tests-with-dependencies.ps1 -Analyze -Filter "Category=Simulation"

.NOTES
    Requires: dotnet CLI, TestDependencyGraph.json in workspace root
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$Filter = "Essential=true",
    
    [Parameter(Mandatory = $false)]
    [int]$MaxDepth = 5,
    
    [switch]$Analyze,
    
    [switch]$ShowDetails
)

# Find workspace root
$workspaceRoot = (Get-Location).Path
while (-not (Test-Path "$workspaceRoot/TestDependencyGraph.json")) {
    $parent = Split-Path $workspaceRoot -Parent
    if ($parent -eq $workspaceRoot) {
        Write-Error "TestDependencyGraph.json not found. Are you in the workspace?"
        exit 1
    }
    $workspaceRoot = $parent
}

# Load test dependency graph
$graphFile = "$workspaceRoot/TestDependencyGraph.json"
if (-not (Test-Path $graphFile)) {
    Write-Error "TestDependencyGraph.json not found at $graphFile"
    exit 1
}

$graphJson = Get-Content $graphFile | ConvertFrom-Json
$testSuite = $graphJson.testSuite

Write-Host "🧪 Chess Policy Essential Tests Runner" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Workspace: $workspaceRoot" -ForegroundColor Gray
Write-Host "Filter: $Filter" -ForegroundColor Gray

# Parse filter into key-value pairs
function Parse-Filter {
    param([string]$FilterString)
    $pairs = @{}
    $FilterString -split '\|' | ForEach-Object {
        if ($_ -match '(\w+)=(.+)') {
            $key = $matches[1]
            $value = $matches[2]
            if (-not $pairs.ContainsKey($key)) {
                $pairs[$key] = @()
            }
            $pairs[$key] += $value
        }
    }
    return $pairs
}

# Check if test matches filter
function Test-MatchesFilter {
    param(
        [PSCustomObject]$Test,
        [hashtable]$FilterCriteria
    )
    
    foreach ($key in $FilterCriteria.Keys) {
        $values = $FilterCriteria[$key]
        
        switch ($key) {
            "Essential" {
                if ($Test.traits.Essential -ne $values[0]) { return $false }
            }
            "Category" {
                if ($values -notcontains $Test.traits.Category) { return $false }
            }
            "ChessRule" {
                $testRules = $Test.traits.ChessRule -split '\|'
                $hasMatch = $false
                foreach ($rule in $values) {
                    if ($testRules -contains $rule) { $hasMatch = $true; break }
                }
                if (-not $hasMatch) { return $false }
            }
            "Layer" {
                # Layer is a top-level property, not in traits
                if ($Test.layer -ne [int]$values[0]) { return $false }
            }
            "DependsOn" {
                $testDeps = $Test.dependsOn
                $hasMatch = $false
                foreach ($dep in $values) {
                    if ($testDeps -contains $dep) { $hasMatch = $true; break }
                }
                if (-not $hasMatch) { return $false }
            }
            "TestId" {
                if ($values -notcontains $Test.testId) { return $false }
            }
        }
    }
    
    return $true
}

# Build dependency graph map
$depGraph = @{}
foreach ($test in $testSuite) {
    $depGraph[$test.testId] = @{
        Test = $test
        DependsOn = $test.dependsOn
        Status = "pending" # pending, passed, failed, skipped
    }
}

# Find tests matching filter
$filterCriteria = Parse-Filter $Filter
$matchingTests = $testSuite | Where-Object { Test-MatchesFilter $_ $filterCriteria }

if ($matchingTests.Count -eq 0) {
    Write-Host "❌ No tests match filter: $Filter" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "📋 Tests matching filter:" -ForegroundColor Yellow
$matchingTests | ForEach-Object { Write-Host "  • $($_.testId) - $($_.name)" -ForegroundColor Gray }

if ($Analyze) {
    Write-Host ""
    Write-Host "🔍 Dependency Analysis:" -ForegroundColor Yellow
    
    foreach ($test in $matchingTests) {
        Write-Host ""
        Write-Host "  [$($test.layer)] $($test.testId)" -ForegroundColor Cyan
        Write-Host "    Name: $($test.name)" -ForegroundColor Gray
        
        if ($test.dependsOn -and $test.dependsOn.Count -gt 0) {
            Write-Host "    Depends on:" -ForegroundColor Yellow
            $test.dependsOn | ForEach-Object {
                Write-Host "      → $_" -ForegroundColor Gray
            }
        } else {
            Write-Host "    No dependencies (foundational)" -ForegroundColor Green
        }
    }
    
    exit 0
}

# Topologically sort matching tests by dependencies
function Get-SortedTests {
    param([PSCustomObject[]]$Tests)
    
    $script:sorted = @()
    $script:visited = @()
    $script:visiting = @()
    
    function Visit {
        param([PSCustomObject]$Test)
        
        if ($script:visited -contains $Test.testId) { return }
        if ($script:visiting -contains $Test.testId) {
            Write-Error "Circular dependency detected at $($Test.testId)"
            exit 1
        }
        
        $script:visiting += $Test.testId
        
        foreach ($depId in $Test.dependsOn) {
            $depTest = $Tests | Where-Object { $_.testId -eq $depId }
            if ($depTest) {
                Visit $depTest
            }
        }
        
        $script:visiting = $script:visiting | Where-Object { $_ -ne $Test.testId }
        $script:visited += $Test.testId
        $script:sorted += $Test
    }
    
    foreach ($test in $Tests) {
        Visit $test
    }
    
    return $script:sorted
}

$sortedTests = Get-SortedTests $matchingTests

Write-Host ""
Write-Host "🏃 Execution Order:" -ForegroundColor Yellow
if ($sortedTests -and $sortedTests.Count -gt 0) {
    $sortedTests | ForEach-Object { Write-Host "  [$($_.layer)] $($_.testId)" -ForegroundColor Gray }
} else {
    Write-Host "  (No tests to run)" -ForegroundColor Gray
}

# Perform single rebuild+republish at the start to avoid DLL lock issues
Write-Host ""
Write-Host "🔨 Preparing test assemblies (rebuild once, then reuse)" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
try {
    $rebuildResult = pwsh -NoProfile -ExecutionPolicy Bypass -File "$workspaceRoot/scripts/spark-testctl.ps1" -RebuildOnly 2>&1
    $rebuildOutput = $rebuildResult | Out-String
    
    # Check if rebuild succeeded
    if ($rebuildOutput -match "rebuilt and republished successfully" -or $rebuildOutput -match "Rebuild and republish complete") {
        Write-Host "✓ Test assemblies prepared successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Rebuild output:" -ForegroundColor Red
        Write-Host $rebuildOutput
        Write-Host "✗ Failed to prepare test assemblies" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "✗ Failed to prepare test assemblies: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "▶️  Running Tests" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow

$passedCount = 0
$failedCount = 0
$skippedCount = 0

foreach ($test in $sortedTests) {
    # Check if all dependencies passed
    $allDepsPass = $true
    $failedDep = $null
    
    foreach ($depId in $test.dependsOn) {
        if ($depGraph[$depId].Status -eq "failed") {
            $allDepsPass = $false
            $failedDep = $depId
            break
        }
    }
    
    if (-not $allDepsPass) {
        Write-Host "⊘ $($test.testId)" -ForegroundColor DarkYellow
        Write-Host "  └─ Skipped (dependency failed: $failedDep)" -ForegroundColor Gray
        $depGraph[$test.testId].Status = "skipped"
        $skippedCount++
        continue
    }
    
    # Run test via Spark runner
    $testName = $test.name
    Write-Host "▶ $($test.testId)" -ForegroundColor Cyan -NoNewline
    Write-Host " - $testName" -ForegroundColor Gray
    
    # Build filter using unique TestId trait
    $testFilter = "TestId=$($test.testId)"
    
    # Run test through spark-testctl.ps1 with -SkipBuild to use cached assemblies
    try {
        $result = pwsh -NoProfile -ExecutionPolicy Bypass -File "$workspaceRoot/scripts/spark-testctl.ps1" -Filter $testFilter -SkipBuild 2>&1
        $resultStr = $result | Out-String
        
        # Check if this specific test passed (look for PASSED line with test method name)
        # The test method name includes the testId pattern
        if ($resultStr -match "\[PASSED\].*$($test.testId)" -and $resultStr -match "Total:\s*(\d+).*Passed:\s*(\d+).*Failed:\s*0") {
            Write-Host "  ✓ PASSED" -ForegroundColor Green
            $depGraph[$test.testId].Status = "passed"
            $passedCount++
        } else {
            Write-Host "  ✗ FAILED" -ForegroundColor Red
            $depGraph[$test.testId].Status = "failed"
            $failedCount++
            
            # Show error output
            if ($ShowDetails) {
                Write-Host "  Error details:" -ForegroundColor Red
                $result | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
            }
        }
    }
    catch {
        Write-Host "  ✗ ERROR" -ForegroundColor Red
        Write-Host "    $_" -ForegroundColor DarkRed
        $depGraph[$test.testId].Status = "failed"
        $failedCount++
    }
}

# Summary
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📊 Test Summary" -ForegroundColor Cyan

$total = $passedCount + $failedCount + $skippedCount
$percent = if ($total -gt 0) { [int]($passedCount * 100 / $total) } else { 0 }

Write-Host "  Passed:  $passedCount/$total" -ForegroundColor Green
Write-Host "  Failed:  $failedCount/$total" -ForegroundColor $(if ($failedCount -gt 0) { 'Red' } else { 'DarkGray' })
Write-Host "  Skipped: $skippedCount/$total" -ForegroundColor $(if ($skippedCount -gt 0) { 'Yellow' } else { 'DarkGray' })
Write-Host "  Success: $percent%" -ForegroundColor $(if ($percent -ge 80) { 'Green' } elseif ($percent -ge 50) { 'Yellow' } else { 'Red' })

# Exit with appropriate code
exit $(if ($failedCount -gt 0) { 1 } else { 0 })
