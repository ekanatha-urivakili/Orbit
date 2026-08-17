#!/usr/bin/env bash
set -Eeuo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

for command_name in podman dotnet npm lsof; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command is not installed: $command_name" >&2
        exit 1
    fi
done

if ! podman info >/dev/null 2>&1; then
    echo "Podman is unavailable. Start Podman Desktop or the Podman machine and try again." >&2
    exit 1
fi

ensure_port_available() {
    local port="$1"
    local service_name="$2"

    if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
        echo "$service_name port $port is currently in use. Attempting to kill the process..." >&2
        local pids
        pids=$(lsof -t -nP -iTCP:"$port" -sTCP:LISTEN || true)
        if [[ -n "$pids" ]]; then
            # Use xargs to pass the PIDs to kill.
            echo "$pids" | xargs kill -9
            echo "Process(es) $pids killed." >&2
            sleep 1
        fi
    fi
}

ensure_port_available 5014 "Orbit API"
ensure_port_available 5173 "Orbit web"

if [[ ! -d web/node_modules ]]; then
    echo "Frontend dependencies are missing. Run 'cd web && npm ci' first." >&2
    exit 1
fi

dotnet tool restore >/dev/null

wait_for_container() {
    local container_name="$1"
    local status=""

    for _ in {1..60}; do
        status="$(podman inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_name" 2>/dev/null || true)"
        if [[ "$status" == "healthy" || "$status" == "running" ]]; then
            return 0
        fi
        sleep 1
    done

    echo "Container did not become healthy: $container_name (status: ${status:-missing})" >&2
    return 1
}

echo "Starting PostgreSQL and Valkey if required..."
./scripts/start-local-services.sh
wait_for_container orbit-postgres-1
wait_for_container orbit-valkey-1
wait_for_container orbit-mailpit-1
wait_for_container orbit-minio-1

echo "Applying database migrations..."
./scripts/migrate.sh

child_pids=()

cleanup() {
    trap - EXIT
    if ((${#child_pids[@]} > 0)); then
        kill "${child_pids[@]}" 2>/dev/null || true
        wait "${child_pids[@]}" 2>/dev/null || true
    fi
}

trap cleanup EXIT
trap 'exit 130' INT TERM

dotnet run --project src/Orbit.Api --urls http://127.0.0.1:5014 &
child_pids+=("$!")

dotnet run --project src/Orbit.Worker &
child_pids+=("$!")

(
    cd web
    NODE_OPTIONS="--max-old-space-size=4096" npm run dev -- --host 127.0.0.1 --strictPort
) &
child_pids+=("$!")

echo "Orbit API: http://127.0.0.1:5014"
echo "Orbit web: http://127.0.0.1:5173"
echo "Press Ctrl+C to stop the API, worker, and web processes. Podman services remain running."

while true; do
    for child_pid in "${child_pids[@]}"; do
        if ! kill -0 "$child_pid" 2>/dev/null; then
            exit_code=0
            wait "$child_pid" || exit_code=$?
            echo "An Orbit process exited with status $exit_code; stopping the remaining processes." >&2
            exit "$exit_code"
        fi
    done
    sleep 1
done
