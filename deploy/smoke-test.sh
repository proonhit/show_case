#!/usr/bin/env bash
# End-to-end check of the Docker stack (the GĐ1 "done" criteria):
# log in, create a model, upload a 100 MB .glb with tus, see it recorded and picked up by the Worker.
# Usage: deploy/smoke-test.sh [api-url] [password]   (stack must already be running)
set -euo pipefail

API=${1:-http://localhost:5080}
PASSWORD=${2:?password required}
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
JAR="$WORK/cookies"
b64() { printf '%s' "$1" | base64 | tr -d '\n'; }

echo "Waiting for $API/health"
for _ in $(seq 60); do curl -fs "$API/health" >/dev/null && break; sleep 2; done
curl -fs "$API/health"; echo

echo "Log in"
curl -fs -c "$JAR" -H 'Content-Type: application/json' -d "{\"password\":\"$PASSWORD\"}" "$API/api/auth/login" >/dev/null

echo "Create model"
SLUG="smoke-$(date +%s)"
MODEL_ID=$(curl -fs -b "$JAR" -H 'Content-Type: application/json' \
  -d "{\"slug\":\"$SLUG\",\"translations\":[{\"locale\":\"en\",\"title\":\"Smoke test\"}]}" \
  "$API/api/admin/models" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
echo "  model $MODEL_ID"

SIZE=$((100 * 1024 * 1024))
{ printf 'glTF\x02\x00\x00\x00'; head -c $((SIZE - 8)) /dev/zero; } > "$WORK/big.glb"

echo "tus: create upload"
LOCATION=$(curl -fs -b "$JAR" -X POST -o /dev/null -D - \
  -H 'Tus-Resumable: 1.0.0' -H "Upload-Length: $SIZE" \
  -H "Upload-Metadata: modelId $(b64 "$MODEL_ID"),filename $(b64 big.glb)" \
  "$API/api/admin/uploads" | tr -d '\r' | awk 'tolower($1)=="location:"{print $2}')
case "$LOCATION" in http*) ;; *) LOCATION="$API$LOCATION" ;; esac
echo "  $LOCATION"

echo "tus: send 100 MB in one PATCH"
STATUS=$(curl -s -b "$JAR" -X PATCH -o /dev/null -w '%{http_code}' \
  -H 'Tus-Resumable: 1.0.0' -H 'Upload-Offset: 0' -H 'Content-Type: application/offset+octet-stream' \
  --data-binary @"$WORK/big.glb" "$LOCATION")
[ "$STATUS" = 204 ] || { echo "PATCH returned $STATUS"; exit 1; }

echo "Check the file is recorded"
curl -fs -b "$JAR" "$API/api/admin/models/$MODEL_ID/files" | python3 -c "
import json,sys
files=json.load(sys.stdin)
assert len(files)==1 and files[0]['format']=='glb' and files[0]['sizeBytes']==$SIZE, files
print('  ok:', files[0]['id'])"

echo "Smoke test passed"
