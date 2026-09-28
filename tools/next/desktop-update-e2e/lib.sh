#!/usr/bin/env bash
# Shared by the scripts of this folder and the release workflow: names and helpers of the
# desktop release. Sourced, never run.
# shellcheck disable=SC2034 # The constants are read by the scripts that source this file.

# Velopack package id: the install folder and the updater's cache are named after it. The
# user data lives elsewhere (Hosting/DataDirectory.cs).
readonly LODB_PACK_ID=LoDb.Desktop
readonly LODB_PACK_TITLE="League of Data Base"
readonly LODB_PACK_AUTHORS="League of Data Base"
readonly LODB_BUNDLE_ID=com.leagueofdatabase.desktop
readonly LODB_DESKTOP_PROJECT=src/LoDb.Desktop/LoDb.Desktop.csproj
readonly LODB_RIDS="win-x64 osx-arm64 osx-x64 linux-x64"

lodb_log() { printf '[desktop-update] %s\n' "$*" >&2; }

lodb_fail() {
  printf '[desktop-update] FAILED: %s\n' "$*" >&2
  exit 1
}

# Velopack channel of a build (ADR 0008): one per RID, "-beta" for the staging builds.
lodb_velopack_channel() {
  local rid="$1" channel="$2"
  case "$channel" in
    stable) printf '%s\n' "$rid" ;;
    beta) printf '%s-beta\n' "$rid" ;;
    *) lodb_fail "unknown channel '$channel' (stable or beta)" ;;
  esac
}

lodb_check_rid() {
  case " $LODB_RIDS " in
    *" $1 "*) ;;
    *) lodb_fail "unknown RID '$1' (one of: $LODB_RIDS)" ;;
  esac
}

# Git tag of a desktop release: the workflow publishes version V under desktop-vV.
lodb_release_tag() { printf 'desktop-v%s\n' "$1"; }

# lodb_installable RID VELOPACK_CHANNEL: the build gate.sh installs, as vpk names it in the
# release (portable zip on Windows and macOS, AppImage on Linux).
lodb_installable() {
  case "$1" in
    linux-*) printf '%s-%s.AppImage\n' "$LODB_PACK_ID" "$2" ;;
    *) printf '%s-%s-Portable.zip\n' "$LODB_PACK_ID" "$2" ;;
  esac
}

# Absolute path of an existing folder, in the form native tools (dotnet, vpk, the app)
# accept: Windows paths under Git Bash (pwd -W), POSIX elsewhere.
lodb_native_path() {
  (cd "$1" && { pwd -W 2>/dev/null || pwd; })
}

# vpk from PATH, or from the default folder of the .NET global tools.
lodb_vpk() {
  if command -v vpk >/dev/null 2>&1; then
    vpk "$@"
  elif [ -x "$HOME/.dotnet/tools/vpk" ]; then
    "$HOME/.dotnet/tools/vpk" "$@"
  else
    lodb_fail "vpk is missing: dotnet tool install -g vpk --version <Velopack version>"
  fi
}

lodb_repo_root() {
  local here
  here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  (cd "$here/../../.." && pwd)
}
