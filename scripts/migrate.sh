#!/usr/bin/env sh
set -eu

dotnet tool restore >/dev/null
dotnet ef database update --project src/Orbit.Infrastructure --startup-project src/Orbit.Infrastructure
