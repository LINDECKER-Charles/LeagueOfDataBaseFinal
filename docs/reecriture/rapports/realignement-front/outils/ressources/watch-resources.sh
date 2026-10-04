#!/usr/bin/env bash
# Emits one line when the machine crosses a pressure threshold (and one when it recovers):
# free RAM, headless browsers and node processes left by agents. Polls every 30 s.
MIN_FREE_GB=${MIN_FREE_GB:-10}
MAX_BROWSERS=${MAX_BROWSERS:-8}
MAX_NODES=${MAX_NODES:-40}
state=''
while true; do
  free=$(awk '/^MemFree:/ { print int($2 / 1048576) }' /proc/meminfo)
  names=$(ps -W 2>/dev/null | awk 'NR > 1 { print tolower($NF) }')
  browsers=$(grep -c 'chrome-headless-shell\|headless_shell' <<<"$names")
  nodes=$(grep -c 'node.exe$' <<<"$names")
  now=''
  [ "$free" -lt "$MIN_FREE_GB" ] && now+="ram libre ${free} Go; "
  [ "$browsers" -gt "$MAX_BROWSERS" ] && now+="${browsers} navigateurs headless; "
  [ "$nodes" -gt "$MAX_NODES" ] && now+="${nodes} processus node; "
  if [ "$now" != "$state" ]; then
    if [ -n "$now" ]; then echo "PRESSION: ${now}(ram ${free} Go, node ${nodes}, headless ${browsers})"
    else echo "RETOUR NORMAL: ram ${free} Go, node ${nodes}, headless ${browsers}"; fi
    state=$now
  fi
  sleep 30
done
