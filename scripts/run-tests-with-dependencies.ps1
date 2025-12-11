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

# Ensure PowerShell outputs UTF-8 so emoji/chess glyphs survive
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
} catch {}

# Helper: emit ANSI-colored text (works when capturing output as bytes)
function Get-AnsiCode {
    param([string]$color)
    switch ($color.ToLower()) {
        'black' { '30' }
        'darkblue' { '34' }
        'darkgreen' { '32' }
        'darkcyan' { '36' }
        'darkred' { '31' }
        'darkmagenta' { '35' }
        'darkyellow' { '33' }
        'gray' { '37' }
        'darkgray' { '90' }
        'blue' { '94' }
        'green' { '92' }
        'cyan' { '96' }
        'red' { '91' }
        'magenta' { '95' }
        'yellow' { '93' }
        'white' { '97' }
        'brightgreen' { '92' }
        default { '0' }
    }
}

function Write-ColorLine {
    param([string]$text, [string]$color)
    try {
        $code = Get-AnsiCode $color
        if ($code -ne '0') { Write-Host "`e[${code}m${text}`e[0m" } else { Write-Host $text }
    } catch { Write-Host $text }
}

function Write-ColorNoNewline {
    param([string]$text, [string]$color)
    try {
        $code = Get-AnsiCode $color
        if ($code -ne '0') { Write-Host -NoNewline "`e[${code}m${text}`e[0m" } else { Write-Host -NoNewline $text }
    } catch { Write-Host -NoNewline $text }
}

# Set default environment variables for Spark integration
$env:SPARK_HOME = "/opt/spark"
$env:DOTNET_WORKER_DIR = "/opt/microsoft-spark-worker"
$env:DOTNETBACKEND_PORT = "5567"
$env:PYTHON_WORKER_FACTORY_PORT = "5567"
$env:DOTNET_WORKER_SPARK_VERSION = "2.3.0"

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

Write-ColorLine "🧪 Chess Policy Essential Tests Runner" "Cyan"
Write-ColorLine "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Cyan"
Write-ColorLine "Workspace: $workspaceRoot" "Gray"
Write-ColorLine "Filter: $Filter" "Gray"

# Spinner disabled to avoid carriage-return artifacts during capture
$green = "`e[32m"; $brightGreen = "`e[92m"; $yellow = "`e[33m"; $magenta = "`e[35m"; $cyan = "`e[36m"; $reset = "`e[0m"
$accumulator = ""
$spinIndex = 0
$spinnerActive = $false
$spinnerClear = ' '.PadRight(80)

function Invoke-CommandWithSpinner {
    param([string]$Command, [string]$Label = "running")
    
    $job = Start-Job -ScriptBlock {
        param($Cmd)
        try {
            Invoke-Expression $Cmd 2>&1
        } catch {
            "ERROR: $_"
        }
    } -ArgumentList $Command
    
    $script:spinIndex = 0
    $script:accumulator = ""
    $script:spinnerActive = $false
    
    while ($true) {
        $jobState = (Get-Job -Id $job.Id).State
        if ($jobState -ne 'Running') { break }
        # spinner disabled: poll and sleep
        Start-Sleep -Milliseconds 300
    }
    
    if ($script:spinnerActive) { Write-Host -NoNewline "`r       `r"; $script:spinnerActive = $false }
    Write-Host "`r$spinnerClear`r"
    
    $result = Receive-Job $job
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    return $result
}

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
    Write-ColorLine "❌ No tests match filter: $Filter" "Red"
    exit 1
}

Write-Host ""
Write-ColorLine "📋 Tests matching filter:" "Yellow"
$matchingTests | ForEach-Object { Write-ColorLine "  • $($_.testId) - $($_.name)" "Gray" }

if ($Analyze) {
    Write-Host ""
    Write-ColorLine "🔍 Dependency Analysis:" "Yellow"
    
    foreach ($test in $matchingTests) {
        Write-Host ""
        Write-ColorLine "  [$($test.layer)] $($test.testId)" "Cyan"
        Write-ColorLine "    Name: $($test.name)" "Gray"
        
        if ($test.dependsOn -and $test.dependsOn.Count -gt 0) {
            Write-ColorLine "    Depends on:" "Yellow"
            $test.dependsOn | ForEach-Object { Write-ColorLine "      → $_" "Gray" }
        } else {
            Write-ColorLine "    No dependencies (foundational)" "Green"
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
Write-ColorLine "🏃 Execution Order:" "Yellow"
if ($sortedTests -and $sortedTests.Count -gt 0) {
    $sortedTests | ForEach-Object { Write-ColorLine "  [$($_.layer)] $($_.testId)" "Gray" }
} else {
    Write-ColorLine "  (No tests to run)" "Gray"
}

# Perform single rebuild+republish at the start to avoid DLL lock issues
Write-Host ""
Write-ColorLine "🔨 Preparing test assemblies (rebuild once, then reuse)" "Yellow"
Write-ColorLine "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Yellow"
try {
    $rebuildCommand = "pwsh -NoProfile -ExecutionPolicy Bypass -File `"$workspaceRoot/scripts/spark-testctl.ps1`" -RebuildOnly"
    $rebuildResult = Invoke-CommandWithSpinner -Command $rebuildCommand -Label "rebuild"
    $rebuildOutput = $rebuildResult | Out-String
    
    # Check if rebuild succeeded
    if ($rebuildOutput -match "rebuilt and republished successfully" -or $rebuildOutput -match "Rebuild and republish complete") {
        Write-ColorLine "✓ Test assemblies prepared successfully" "Green"
    } else {
        Write-ColorLine "✗ Rebuild output:" "Red"
        Write-Host $rebuildOutput
        Write-ColorLine "✗ Failed to prepare test assemblies" "Red"
        exit 1
    }
}
catch {
    Write-ColorLine "✗ Failed to prepare test assemblies: $_" "Red"
    exit 1
}

Write-Host ""
Write-ColorLine "▶️  Running Tests" "Yellow"
Write-ColorLine "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Yellow"

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
        Write-ColorLine "⊘ $($test.testId)" "DarkYellow"
        Write-ColorLine "  └─ Skipped (dependency failed: $failedDep)" "Gray"
        $depGraph[$test.testId].Status = "skipped"
        $skippedCount++
        continue
    }
    
    # Run test via Spark runner
    $testName = $test.name
    Write-ColorNoNewline "▶ $($test.testId)" "Cyan"
    Write-ColorLine " - $testName" "Gray"
    
    # Build filter using unique TestId trait
    $testFilter = "TestId=$($test.testId)"
    
    # Run test through spark-testctl.ps1 with -SkipBuild to use cached assemblies
    try {
        $testCommand = "pwsh -NoProfile -ExecutionPolicy Bypass -File `"$workspaceRoot/scripts/spark-testctl.ps1`" -Filter $testFilter -SkipBuild"
        $result = Invoke-CommandWithSpinner -Command $testCommand -Label "test $($test.testId)"
        $resultStr = $result | Out-String
        
        # Check if this specific test passed (look for PASSED line with test method name)
        # The test method name includes the testId pattern
        if ($resultStr -match "\[PASSED\].*$($test.testId)" -and $resultStr -match "Total:\s*(\d+).*Passed:\s*(\d+).*Failed:\s*0") {
            Write-ColorLine "  ✓ PASSED" "Green"
            $depGraph[$test.testId].Status = "passed"
            $passedCount++
        } else {
            Write-ColorLine "  ✗ FAILED" "Red"
            $depGraph[$test.testId].Status = "failed"
            $failedCount++
            
            # Show error output
            if ($ShowDetails) {
                Write-ColorLine "  Error details:" "Red"
                $result | ForEach-Object { Write-ColorLine "    $_" "DarkRed" }
            }
        }
    }
    catch {
        Write-ColorLine "  ✗ ERROR" "Red"
        Write-ColorLine "    $_" "DarkRed"
        $depGraph[$test.testId].Status = "failed"
        $failedCount++
    }
}

# Summary
Write-Host ""
Write-ColorLine "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" "Cyan"
Write-ColorLine "📊 Test Summary" "Cyan"

$total = $passedCount + $failedCount + $skippedCount
$percent = if ($total -gt 0) { [int]($passedCount * 100 / $total) } else { 0 }

$failedColor = if ($failedCount -gt 0) { 'Red' } else { 'DarkGray' }
$skippedColor = if ($skippedCount -gt 0) { 'Yellow' } else { 'DarkGray' }
$successColor = if ($percent -ge 80) { 'Green' } elseif ($percent -ge 50) { 'Yellow' } else { 'Red' }

Write-ColorLine "  Passed:  $passedCount/$total" "Green"
Write-ColorLine "  Failed:  $failedCount/$total" $failedColor
Write-ColorLine "  Skipped: $skippedCount/$total" $skippedColor
Write-ColorLine "  Success: $percent%" $successColor

# Show cursor
Write-Host -NoNewline "`e[?25h"

# Exit with appropriate code
exit $(if ($failedCount -gt 0) { 1 } else { 0 })
