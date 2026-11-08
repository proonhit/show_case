#!/usr/bin/env bash
# Checks the processing pipeline in the Docker stack (GĐ2):
#   1. a real .fbx (made by Blender inside the worker: a subdivided monkey, ~250K triangles)
#      → convert → stats → optimize + phone copy, all steps done
#   2. a damaged .glb → the job fails at the stats step with a readable message
# Usage: deploy/pipeline-test.sh [api-url] [password]   (stack must be running)
set -euo pipefail

API=${1:-http://localhost:5080}
PASSWORD=${2:?password required}
COMPOSE="docker compose -f deploy/docker-compose.dev.yml --env-file deploy/.env"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
JAR="$WORK/cookies"
b64() { printf '%s' "$1" | base64 | tr -d '\n'; }
json() { python3 -c "import json,sys; d=json.load(sys.stdin); print(eval(sys.argv[1]))" "$1"; }

curl -fs -c "$JAR" -H 'Content-Type: application/json' -d "{\"password\":\"$PASSWORD\"}" "$API/api/auth/login" >/dev/null

new_model() {
  curl -fs -b "$JAR" -H 'Content-Type: application/json' \
    -d "{\"slug\":\"$1-$(date +%s%N | tail -c 7)\",\"translations\":[{\"locale\":\"en\",\"title\":\"$1\"}]}" \
    "$API/api/admin/models" | json "d['id']"
}

upload() { # model-id file name
  local size; size=$(stat -c %s "$2")
  local loc; loc=$(curl -fs -b "$JAR" -X POST -o /dev/null -D - -H 'Tus-Resumable: 1.0.0' -H "Upload-Length: $size" \
    -H "Upload-Metadata: modelId $(b64 "$1"),filename $(b64 "$3")" "$API/api/admin/uploads" | tr -d '\r' | awk 'tolower($1)=="location:"{print $2}')
  case "$loc" in http*) ;; *) loc="$API$loc" ;; esac
  curl -fs -b "$JAR" -X PATCH -o /dev/null -H 'Tus-Resumable: 1.0.0' -H 'Upload-Offset: 0' \
    -H 'Content-Type: application/offset+octet-stream' --data-binary @"$2" "$loc"
}

wait_job() { # model-id → prints the final job JSON
  for _ in $(seq 120); do
    local job; job=$(curl -fs -b "$JAR" "$API/api/admin/models/$1/jobs/latest" || true)
    case "$(printf '%s' "$job" | json "d['status']" 2>/dev/null)" in Succeeded|Failed) printf '%s' "$job"; return ;; esac
    sleep 2
  done
  echo "job did not finish in 4 minutes" >&2; exit 1
}

echo "== .fbx made by Blender in the worker"
$COMPOSE exec -T worker blender -b --factory-startup --python-expr "
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_monkey_add()
m = bpy.context.object.modifiers.new('sub', 'SUBSURF'); m.levels = 4; m.render_levels = 4
mat = bpy.data.materials.new('Paint'); mat.use_nodes = True; bpy.context.object.data.materials.append(mat)
bpy.ops.export_scene.fbx(filepath='/data/work/monkey.fbx', use_mesh_modifiers=True)
" >/dev/null
$COMPOSE cp worker:/data/work/monkey.fbx "$WORK/monkey.fbx"
ls -lh "$WORK/monkey.fbx"
MODEL=$(new_model fbx-test)
upload "$MODEL" "$WORK/monkey.fbx" monkey.fbx
JOB=$(wait_job "$MODEL")
printf '%s' "$JOB" | python3 -m json.tool
printf '%s' "$JOB" | json "d['status']" | grep -qx Succeeded
printf '%s' "$JOB" | json "[s['status'] for s in d['steps']]" | grep -qx "\['done', 'done', 'done', 'done'\]"
FILES=$(curl -fs -b "$JAR" "$API/api/admin/models/$MODEL/files")
printf '%s' "$FILES" | json "sorted(f['kind'] for f in d if f['isCurrent'])" | grep -qx "\['Mobile', 'Optimized', 'Original'\]"
DETAIL=$(curl -fs -b "$JAR" "$API/api/admin/models/$MODEL")
echo "  files: $(printf '%s' "$FILES" | json "[(f['kind'], f['format'], f['sizeBytes']) for f in d]")"

echo "== damaged .glb"
{ printf 'glTF\x02\x00\x00\x00'; head -c 4096 /dev/urandom; } > "$WORK/broken.glb"
MODEL2=$(new_model broken-test)
upload "$MODEL2" "$WORK/broken.glb" broken.glb
JOB2=$(wait_job "$MODEL2")
printf '%s' "$JOB2" | json "d['status'], d['error']"
printf '%s' "$JOB2" | json "d['status']" | grep -qx Failed
printf '%s' "$JOB2" | json "[s['name'] for s in d['steps'] if s['status']=='failed']" | grep -qx "\['stats'\]"

echo "Pipeline test passed"
