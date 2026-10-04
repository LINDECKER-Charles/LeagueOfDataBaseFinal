# Samples the HDD holding F: (repo and worktrees) every 5 s. Writes 1/0 into the flag file read
# by heavy.sh (1 = saturated over the last 30 s, with hysteresis) and prints one line on each
# transition, naming the processes doing the most I/O at that moment.
param(
  [string]$Flag = 'C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp/disk-busy',
  [string]$Disk = '0 E: F:',
  [int]$IntervalSeconds = 5,
  [int]$Window = 6,
  [double]$EnterBusy = 85,
  [double]$LeaveBusy = 60,
  [int]$ReportAfterSeconds = 120,
  # Per-process I/O counters include pipes and sockets (a browser streaming screenshots to
  # Playwright, a preview proxying): only tools whose I/O is mostly files are named here.
  [string]$Ours = '^(esbuild|git|rg|dotnet|MSBuild|VBCSCompiler)(#\d+)? '
)
$samples = [System.Collections.Generic.Queue[double]]::new()
$saturated = $false
$reported = $false
$since = Get-Date

function TopIo {
  Get-CimInstance Win32_PerfFormattedData_PerfProc_Process |
    Where-Object { $_.Name -notin '_Total', 'Idle' } |
    Sort-Object { $_.IOWriteBytesPersec + $_.IOReadBytesPersec } -Descending |
    Select-Object -First 5 |
    ForEach-Object { '{0} w{1:N0}/r{2:N0} Ko/s' -f $_.Name, ($_.IOWriteBytesPersec / 1KB), ($_.IOReadBytesPersec / 1KB) }
}

while ($true) {
  $d = Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk -Filter "Name='$Disk'"
  $samples.Enqueue(100 - [double]$d.PercentIdleTime)
  if ($samples.Count -gt $Window) { [void]$samples.Dequeue() }
  $average = ($samples | Measure-Object -Average).Average
  $now = if ($saturated) { $average -gt $LeaveBusy } else { $average -gt $EnterBusy }
  [System.IO.File]::WriteAllText($Flag, [string][int]$now)
  # The flag follows every transition; a line is printed only when it is worth acting on: our
  # own processes among the top I/O, or a saturation lasting over $ReportAfterSeconds.
  if ($now -and -not $saturated) { $since = Get-Date; $reported = $false }
  if ($now -and -not $reported) {
    $top = TopIo
    $lasting = ((Get-Date) - $since).TotalSeconds -ge $ReportAfterSeconds
    if ($lasting -or ($top -match $Ours)) {
      'DISQUE F: SATURE depuis {0:N0} s ({1:N0} % occupe sur 30 s) ; top I/O : {2}' -f ((Get-Date) - $since).TotalSeconds, $average, ($top -join ' ; ')
      $reported = $true
    }
  }
  if ($saturated -and -not $now -and $reported) {
    'DISQUE F: RETOUR NORMAL ({0:N0} % occupe sur 30 s)' -f $average
  }
  $saturated = $now
  Start-Sleep -Seconds $IntervalSeconds
}
