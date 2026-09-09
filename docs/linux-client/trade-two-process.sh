#!/usr/bin/env bash
# Two Client.Linux sessions on one Server.Linux — player trade evidence.
# Requires Server.Linux already LISTEN on 7000 (Jev --root, --test-server, --allow-start-game).
# Do not TCP-probe 7000. Host (linux2 / LinuxWar2) accepts; guest (linux / LinuxWar) requests + gold.
# Jev MaxIP defaults to 5; after this pair logs out, wait or restart Server.Linux before another connect.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
DOTNET="${DOTNET:-dotnet}"
CATALOG="${CATALOG:-${ROOT}/Tools/Crystal.Bake/fixtures/bake-out/catalog.json}"
MAPS="${MAPS:-/tmp/Crystal.Database/Jev/Maps}"
INI="${INI:-${ROOT}/Client.Linux/Mir2Test.ini}"
OUT="${OUT:-/tmp/crystal-trade}"
mkdir -p "${OUT}"

run_client() {
  "${DOTNET}" run --project "${ROOT}/Client.Linux/Client.Linux.csproj" -c Release --no-build -- \
    --connect --headless --no-gate --no-walk \
    --ini "${INI}" \
    --catalog "${CATALOG}" \
    --maps "${MAPS}" \
    "$@"
}

echo "host → ${OUT}/host.log"
run_client \
  --account linux2 --password linux1 --character LinuxWar2 --new-account \
  --auto-trade-reply --auto-trade-confirm \
  --input-script 'AllowTrade,Move:300:616,Face:Left,Wait:22000,TradeConfirm' \
  --keep-alive 4000 \
  > "${OUT}/host.log" 2>&1 &
HOST_PID=$!

sleep 6

echo "guest → ${OUT}/guest.log"
set +e
run_client \
  --account linux --password linux1 --character LinuxWar \
  --input-script 'AllowTrade,Move:299:616,Face:Right,Wait:800,Trade,TradeGold:50,TradeConfirm,Wait:8000' \
  --keep-alive 2000 \
  > "${OUT}/guest.log" 2>&1
GUEST_EC=$?
wait "${HOST_PID}"
HOST_EC=$?
set -e

echo "HOST_EXIT:${HOST_EC}"
echo "GUEST_EXIT:${GUEST_EC}"
echo "=== host trade lines ==="
grep -E 'TradeRequest|TradeReply|TradeAccept|TradeGold|TradeConfirm|TradeHandshake|TradeDone|hud-trade|input Trade|ObjectPlayer|AllowTrade|ChangeTrade' "${OUT}/host.log" | tail -n 40 || true
echo "=== guest trade lines ==="
grep -E 'TradeRequest|TradeReply|TradeAccept|TradeGold|TradeConfirm|TradeHandshake|TradeDone|hud-trade|input Trade|ObjectPlayer|AllowTrade|ChangeTrade|LoseGold' "${OUT}/guest.log" | tail -n 40 || true
exit "${GUEST_EC}"
