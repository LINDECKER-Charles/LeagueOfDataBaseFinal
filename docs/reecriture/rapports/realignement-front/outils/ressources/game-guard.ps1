# Game mode for what heavy.sh cannot gate: the agents' own Playwright scripts, browsers and
# previews. While a League of Legends match runs, they are set to Idle priority (never
# suspended: a frozen browser would break the agents' scripts); after it, to BelowNormal.
# Only processes of this work are touched, recognised by their command line. Prints one line
# per match start and end.
param([int]$IntervalSeconds = 10)
$ours = 'lodb-parite|LeagueOfDataBaseFinal|scratchpad[\\/]cmp|ms-playwright'
$inMatch = $null

function Set-OurPriority([string]$Priority) {
  $count = 0
  Get-CimInstance Win32_Process -Filter "Name='node.exe' OR Name='chrome-headless-shell.exe' OR Name='chrome.exe' OR Name='esbuild.exe'" |
    Where-Object { $_.CommandLine -match $ours } |
    ForEach-Object {
      $process = Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue
      if ($process -and $process.PriorityClass -ne $Priority) {
        try { $process.PriorityClass = $Priority; $count++ } catch { }
      }
    }
  $count
}

while ($true) {
  $now = [bool](Get-Process -Name 'League of Legends' -ErrorAction SilentlyContinue)
  if ($now) {
    $lowered = Set-OurPriority 'Idle'
    if ($inMatch -ne $true) { "PARTIE EN COURS: mode jeu actif ($lowered processus des agents passes en Idle)" }
  } elseif ($inMatch -eq $true) {
    $raised = Set-OurPriority 'BelowNormal'
    "PARTIE TERMINEE: reprise ($raised processus remis en BelowNormal)"
  }
  $inMatch = $now
  Start-Sleep -Seconds $IntervalSeconds
}
