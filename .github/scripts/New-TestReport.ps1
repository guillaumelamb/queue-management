param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$Branch,

    [Parameter(Mandatory = $true)]
    [string]$Commit,

    [Parameter(Mandatory = $true)]
    [string]$RunNumber,

    [Parameter(Mandatory = $true)]
    [string]$WorkflowUrl,

    [string]$RestoreOutcome = "skipped",
    [string]$BuildOutcome = "skipped",
    [string]$TestOutcome = "skipped",
    [string]$BenchmarkOutcome = "skipped"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedOutcome {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return "skipped"
    }

    return $Value.ToLowerInvariant()
}

function Get-OutcomeClass {
    param([string]$Value)

    switch (Get-NormalizedOutcome $Value) {
        "success" { return "success" }
        "passed" { return "success" }
        "failure" { return "failure" }
        "failed" { return "failure" }
        "skipped" { return "neutral" }
        default { return "neutral" }
    }
}

function Format-Duration {
    param([TimeSpan]$Value)

    if ($Value -le [TimeSpan]::Zero) {
        return "0s"
    }

    if ($Value.TotalHours -ge 1) {
        return "{0:h\:mm\:ss}" -f $Value
    }

    if ($Value.TotalMinutes -ge 1) {
        return "{0:m\:ss}" -f $Value
    }

    return "{0:N2}s" -f $Value.TotalSeconds
}

function Escape-Html {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return [System.Net.WebUtility]::HtmlEncode($Value)
}

function Get-TestRecords {
    param([System.Xml.XmlDocument]$Document)

    $definitions = @{}

    foreach ($unitTest in @($Document.TestRun.TestDefinitions.UnitTest)) {
        if ($null -eq $unitTest) {
            continue
        }

        $className = ""
        if ($null -ne $unitTest.TestMethod -and $null -ne $unitTest.TestMethod.className) {
            $className = [string]$unitTest.TestMethod.className
        }

        $definitions[[string]$unitTest.id] = [ordered]@{
            Name = [string]$unitTest.name
            ClassName = $className
        }
    }

    $records = New-Object System.Collections.Generic.List[object]

    foreach ($result in @($Document.TestRun.Results.UnitTestResult)) {
        if ($null -eq $result) {
            continue
        }

        $definition = $definitions[[string]$result.testId]
        $name = if ($null -ne $definition) { $definition.Name } else { [string]$result.testName }
        $className = if ($null -ne $definition) { $definition.ClassName } else { "" }
        $duration = [string]$result.duration
        $message = ""
        $stackTrace = ""

        $outputNode = $null
        if ($result.PSObject.Properties.Name -contains "Output") {
            $outputNode = $result.Output
        }

        if ($null -ne $outputNode) {
            $errorInfoNode = $null
            if ($outputNode.PSObject.Properties.Name -contains "ErrorInfo") {
                $errorInfoNode = $outputNode.ErrorInfo
            }

            if ($null -ne $errorInfoNode) {
                if ($errorInfoNode.PSObject.Properties.Name -contains "Message" -and $null -ne $errorInfoNode.Message) {
                    $message = [string]$errorInfoNode.Message
                }

                if ($errorInfoNode.PSObject.Properties.Name -contains "StackTrace" -and $null -ne $errorInfoNode.StackTrace) {
                    $stackTrace = [string]$errorInfoNode.StackTrace
                }
            }
        }

        $records.Add([pscustomobject]@{
            Name = $name
            ClassName = $className
            Outcome = [string]$result.outcome
            Duration = $duration
            Message = $message
            StackTrace = $stackTrace
        })
    }

    return $records
}

$restoreOutcome = Get-NormalizedOutcome $RestoreOutcome
$buildOutcome = Get-NormalizedOutcome $BuildOutcome
$testOutcome = Get-NormalizedOutcome $TestOutcome
$benchmarkOutcome = Get-NormalizedOutcome $BenchmarkOutcome
$overallStatus = if ($restoreOutcome -eq "success" -and $buildOutcome -eq "success" -and $testOutcome -eq "success" -and $benchmarkOutcome -eq "success") { "Passing" } else { "Failing" }
$overallClass = if ($overallStatus -eq "Passing") { "success" } else { "failure" }

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$outputRoot = [System.IO.Path]::GetFullPath($OutputDir)

$trxFiles = @()
if (Test-Path -LiteralPath $ResultsDir) {
    $trxFiles = @(
        Get-ChildItem -Path $ResultsDir -Filter *.trx -File -Recurse |
        Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -notlike "$outputRoot*" } |
        Sort-Object LastWriteTime -Descending
    )
}

$rawDir = Join-Path $OutputDir "raw"
if ($trxFiles.Count -gt 0) {
    New-Item -ItemType Directory -Path $rawDir -Force | Out-Null

    foreach ($file in $trxFiles) {
        Copy-Item -Path $file.FullName -Destination (Join-Path $rawDir $file.Name) -Force
    }
}

$total = 0
$passed = 0
$failed = 0
$skipped = 0
$duration = [TimeSpan]::Zero
$tests = New-Object System.Collections.Generic.List[object]

foreach ($trxFile in $trxFiles) {
    [xml]$document = Get-Content -Path $trxFile.FullName -Raw

    if ($null -ne $document.TestRun.ResultSummary -and $null -ne $document.TestRun.ResultSummary.Counters) {
        $counters = $document.TestRun.ResultSummary.Counters
        $total += [int]$counters.total
        $passed += [int]$counters.passed
        $failed += [int]$counters.failed
        $skipped += [int]$counters.notExecuted
    }

    if ($null -ne $document.TestRun.Times -and $null -ne $document.TestRun.Times.start -and $null -ne $document.TestRun.Times.finish) {
        $startTime = [DateTimeOffset]::Parse([string]$document.TestRun.Times.start)
        $finishTime = [DateTimeOffset]::Parse([string]$document.TestRun.Times.finish)
        $duration += ($finishTime - $startTime)
    }

    foreach ($record in Get-TestRecords -Document $document) {
        $tests.Add($record)
    }
}

$shortCommit = if ($Commit.Length -gt 7) { $Commit.Substring(0, 7) } else { $Commit }
$generatedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
$durationLabel = if ($trxFiles.Count -gt 0) { Format-Duration $duration } else { "Not available" }
$rawReportLink = if ($trxFiles.Count -gt 0) { '<a href="./raw/">Open raw TRX files</a>' } else { "No TRX file was produced." }
$failedTests = @($tests | Where-Object { $_.Outcome -eq "Failed" } | Sort-Object ClassName, Name)
$allTests = @($tests | Sort-Object ClassName, Name)

$failureMarkup = if ($failedTests.Count -eq 0) {
    '<p class="empty-state">No failing tests were recorded.</p>'
}
else {
    ($failedTests | ForEach-Object {
        $details = @()

        if (-not [string]::IsNullOrWhiteSpace($_.Message)) {
            $details += "<pre>$(Escape-Html $_.Message)</pre>"
        }

        if (-not [string]::IsNullOrWhiteSpace($_.StackTrace)) {
            $details += "<pre>$(Escape-Html $_.StackTrace)</pre>"
        }

        @"
<article class="failure-card">
  <h3>$(Escape-Html $_.Name)</h3>
  <p class="meta">$(Escape-Html $_.ClassName)</p>
  $($details -join "`n")
</article>
"@
    }) -join "`n"
}

$testRows = if ($allTests.Count -eq 0) {
    '<tr><td colspan="4">No test case details were recorded.</td></tr>'
}
else {
    ($allTests | ForEach-Object {
        @"
<tr>
  <td>$(Escape-Html $_.Name)</td>
  <td>$(Escape-Html $_.ClassName)</td>
  <td><span class="pill $(Get-OutcomeClass $_.Outcome)">$(Escape-Html $_.Outcome)</span></td>
  <td>$(Escape-Html $_.Duration)</td>
</tr>
"@
    }) -join "`n"
}

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>$Repository Test Report</title>
  <style>
    :root {
      --bg: #f4f1ea;
      --panel: #fffdf9;
      --text: #1f2933;
      --muted: #5b6672;
      --line: #d9cfbf;
      --success: #17633a;
      --success-bg: #e3f3e8;
      --failure: #8c1d18;
      --failure-bg: #fde8e6;
      --neutral: #68563a;
      --neutral-bg: #f3ead6;
      --accent: #0f4c5c;
      --shadow: 0 16px 40px rgba(15, 28, 42, 0.08);
    }

    * {
      box-sizing: border-box;
    }

    body {
      margin: 0;
      font-family: "Segoe UI", Arial, sans-serif;
      background:
        radial-gradient(circle at top right, rgba(15, 76, 92, 0.12), transparent 26rem),
        linear-gradient(180deg, #f9f6ef 0%, var(--bg) 100%);
      color: var(--text);
    }

    main {
      max-width: 1100px;
      margin: 0 auto;
      padding: 32px 20px 48px;
    }

    .hero {
      background: linear-gradient(135deg, rgba(15, 76, 92, 0.98), rgba(39, 91, 72, 0.92));
      color: #f7fbfc;
      border-radius: 24px;
      padding: 28px;
      box-shadow: var(--shadow);
    }

    .hero h1 {
      margin: 0 0 8px;
      font-size: clamp(2rem, 5vw, 3.1rem);
      line-height: 1.05;
    }

    .hero p {
      margin: 0;
      color: rgba(247, 251, 252, 0.86);
      max-width: 52rem;
    }

    .pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      font-size: 0.9rem;
      font-weight: 600;
    }

    .success {
      color: var(--success);
      background: var(--success-bg);
    }

    .failure {
      color: var(--failure);
      background: var(--failure-bg);
    }

    .neutral {
      color: var(--neutral);
      background: var(--neutral-bg);
    }

    .summary-grid,
    .detail-grid {
      display: grid;
      gap: 18px;
      margin-top: 24px;
    }

    .summary-grid {
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    }

    .detail-grid {
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    }

    .card {
      background: var(--panel);
      border: 1px solid rgba(217, 207, 191, 0.75);
      border-radius: 20px;
      padding: 20px;
      box-shadow: var(--shadow);
    }

    .card h2,
    .card h3 {
      margin-top: 0;
    }

    .metric {
      font-size: 2rem;
      font-weight: 700;
      margin: 10px 0 4px;
    }

    .muted {
      color: var(--muted);
    }

    .step-list {
      display: grid;
      gap: 12px;
    }

    .step-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      padding-bottom: 12px;
      border-bottom: 1px solid var(--line);
    }

    .step-row:last-child {
      border-bottom: 0;
      padding-bottom: 0;
    }

    a {
      color: var(--accent);
      font-weight: 600;
      text-decoration: none;
    }

    a:hover {
      text-decoration: underline;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      margin-top: 12px;
    }

    th,
    td {
      text-align: left;
      padding: 12px 10px;
      border-bottom: 1px solid var(--line);
      vertical-align: top;
    }

    th {
      color: var(--muted);
      font-size: 0.85rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    pre {
      margin: 0;
      padding: 14px;
      background: #13202b;
      color: #eff5f8;
      border-radius: 14px;
      overflow-x: auto;
      white-space: pre-wrap;
      word-break: break-word;
      font-family: Consolas, "Courier New", monospace;
      font-size: 0.9rem;
    }

    .failure-card {
      padding: 18px;
      border: 1px solid rgba(140, 29, 24, 0.2);
      border-radius: 18px;
      background: rgba(253, 232, 230, 0.55);
    }

    .failure-card + .failure-card {
      margin-top: 14px;
    }

    .meta {
      margin: -6px 0 12px;
      color: var(--muted);
    }

    .empty-state {
      margin: 0;
      color: var(--muted);
    }

    footer {
      margin-top: 28px;
      color: var(--muted);
      font-size: 0.95rem;
    }

    @media (max-width: 720px) {
      main {
        padding: 20px 14px 32px;
      }

      .hero,
      .card {
        border-radius: 18px;
      }

      .step-row {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <span class="pill $overallClass">$overallStatus</span>
      <h1>Build and Test Report</h1>
      <p>Generated from GitHub Actions run #$(Escape-Html $RunNumber) for $(Escape-Html $Repository) on branch <strong>$(Escape-Html $Branch)</strong> at commit <strong>$(Escape-Html $shortCommit)</strong>.</p>
    </section>

    <section class="summary-grid">
      <article class="card">
        <h2>Total tests</h2>
        <div class="metric">$total</div>
        <p class="muted">Duration: $durationLabel</p>
      </article>
      <article class="card">
        <h2>Passed</h2>
        <div class="metric">$passed</div>
        <p class="muted">Functional test cases completed successfully.</p>
      </article>
      <article class="card">
        <h2>Failed</h2>
        <div class="metric">$failed</div>
        <p class="muted">Review the failure details below.</p>
      </article>
      <article class="card">
        <h2>Skipped</h2>
        <div class="metric">$skipped</div>
        <p class="muted">Tests marked as not executed in TRX.</p>
      </article>
    </section>

    <section class="detail-grid">
      <article class="card">
        <h2>Pipeline steps</h2>
        <div class="step-list">
          <div class="step-row">
            <div>
              <strong>Restore</strong>
              <div class="muted">dotnet restore QueueManagement.sln</div>
            </div>
            <span class="pill $(Get-OutcomeClass $restoreOutcome)">$restoreOutcome</span>
          </div>
          <div class="step-row">
            <div>
              <strong>Build</strong>
              <div class="muted">dotnet build QueueManagement.sln --configuration Release --no-restore</div>
            </div>
            <span class="pill $(Get-OutcomeClass $buildOutcome)">$buildOutcome</span>
          </div>
          <div class="step-row">
            <div>
              <strong>Test</strong>
              <div class="muted">dotnet test QueueManagement.sln --configuration Release --no-build --filter "Category!=Benchmark"</div>
            </div>
            <span class="pill $(Get-OutcomeClass $testOutcome)">$testOutcome</span>
          </div>
          <div class="step-row">
            <div>
              <strong>Benchmark tests</strong>
              <div class="muted">dotnet test QueueManagement.sln --configuration Release --no-build --filter "Category=Benchmark"</div>
            </div>
            <span class="pill $(Get-OutcomeClass $benchmarkOutcome)">$benchmarkOutcome</span>
          </div>
        </div>
      </article>
      <article class="card">
        <h2>Metadata</h2>
        <p><strong>Workflow run:</strong> <a href="$(Escape-Html $WorkflowUrl)">Open in GitHub Actions</a></p>
        <p><strong>Commit:</strong> <code>$(Escape-Html $Commit)</code></p>
        <p><strong>Report generated:</strong> $generatedAt</p>
        <p><strong>Artifacts:</strong> $rawReportLink</p>
      </article>
    </section>

    <section class="card">
      <h2>Failures</h2>
      $failureMarkup
    </section>

    <section class="card">
      <h2>All recorded tests</h2>
      <table>
        <thead>
          <tr>
            <th>Test</th>
            <th>Class</th>
            <th>Outcome</th>
            <th>Duration</th>
          </tr>
        </thead>
        <tbody>
          $testRows
        </tbody>
      </table>
    </section>

    <footer>
      This page is published from GitHub Actions through GitHub Pages. It is updated on pushes to the repository default branch.
    </footer>
  </main>
</body>
</html>
"@

$html | Out-File -FilePath (Join-Path $OutputDir "index.html") -Encoding utf8
