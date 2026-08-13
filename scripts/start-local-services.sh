#!/usr/bin/env sh
set -eu

podman compose -f deploy/podman/compose.yaml up -d
