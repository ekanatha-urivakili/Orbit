#!/usr/bin/env bash
# Flags destructive schema operations introduced by new EF migrations in a pull request.
#
# Scope is deliberately bounded to the migrations added since $BASE_SHA: it asks EF to
# generate SQL for exactly that range (`dotnet ef migrations script <From> <To>`), not the
# full idempotent history, so a past destructive migration never re-triggers this check.
#
# Enforcement: set MIGRATION_SAFETY_ENFORCE=true to fail on an unapproved destructive
# operation. Left unset (informational phase, see CI-CD-PLAN.md Phase 2), the script still
# prints ::warning:: annotations and a job summary but always exits 0.
set -Eeuo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

base_sha="${BASE_SHA:?BASE_SHA env var is required (the PR base commit)}"
enforce="${MIGRATION_SAFETY_ENFORCE:-false}"
migrations_dir="src/Orbit.Infrastructure/Persistence/Migrations"
exceptions_dir=".github/migration-exceptions"
summary="${GITHUB_STEP_SUMMARY:-/dev/stdout}"

new_migration_files="$(git diff --name-only --diff-filter=A "${base_sha}...HEAD" -- "${migrations_dir}/*.cs" | grep -v '\.Designer\.cs$' || true)"

if [[ -z "${new_migration_files}" ]]; then
    echo "No new EF migrations in this change. Nothing to check." >> "${summary}"
    exit 0
fi

new_migration_names=()
while IFS= read -r file; do
    base_name="$(basename "${file}" .cs)"
    new_migration_names+=("${base_name}")
done <<< "${new_migration_files}"

# Migration filenames are timestamp-prefixed, so lexicographic sort is chronological order.
IFS=$'\n' sorted_new=($(sort <<<"${new_migration_names[*]}"))
unset IFS
earliest_new="${sorted_new[0]}"
latest_new="${sorted_new[$((${#sorted_new[@]} - 1))]}"

full_list="$(dotnet ef migrations list \
    --project src/Orbit.Infrastructure --startup-project src/Orbit.Infrastructure \
    --no-build --configuration Release --no-color --no-connect \
    | grep -E '^[0-9]{14}_')"

from_migration="0"
while IFS= read -r name; do
    if [[ "${name}" == "${earliest_new}" ]]; then
        break
    fi
    from_migration="${name}"
done <<< "${full_list}"

echo "Generating SQL for migration range: ${from_migration} -> ${latest_new}"
range_sql="$(mktemp)"
dotnet ef migrations script "${from_migration}" "${latest_new}" \
    --project src/Orbit.Infrastructure --startup-project src/Orbit.Infrastructure \
    --no-build --configuration Release -o "${range_sql}"

destructive_pattern='DROP[[:space:]]+(TABLE|COLUMN)|RENAME[[:space:]]+(COLUMN|TO)|TRUNCATE|ALTER[[:space:]]+TABLE[[:space:]]+[^;]*ALTER[[:space:]]+COLUMN[^;]*SET[[:space:]]+NOT[[:space:]]+NULL'

findings=()
while IFS= read -r line; do
    findings+=("${line}")
done < <(grep -inE "${destructive_pattern}" "${range_sql}" || true)

{
    echo "### Migration safety check"
    echo
    echo "Range checked: \`${from_migration}\` -> \`${latest_new}\`"
    echo
    echo "New migrations in this PR:"
    for name in "${sorted_new[@]}"; do
        echo "- \`${name}\`"
    done
    echo
} >> "${summary}"

if [[ "${#findings[@]}" -eq 0 ]]; then
    echo "No destructive operations detected." >> "${summary}"
    rm -f "${range_sql}"
    exit 0
fi

echo "| Line | Statement | Exception on file? |" >> "${summary}"
echo "|---|---|---|" >> "${summary}"

unapproved_count=0
for finding in "${findings[@]}"; do
    line_no="${finding%%:*}"
    statement="${finding#*:}"
    approved="no"
    for name in "${sorted_new[@]}"; do
        exception_file="${exceptions_dir}/${name}.md"
        if [[ -f "${exception_file}" ]] && grep -qi "prior expand release" "${exception_file}" \
            && grep -qi "rollback plan" "${exception_file}" \
            && grep -qi "approving owner" "${exception_file}"; then
            approved="yes (${exception_file})"
        fi
    done
    escaped_statement="$(echo "${statement}" | sed 's/|/\\|/g')"
    echo "| ${line_no} | \`${escaped_statement}\` | ${approved} |" >> "${summary}"
    if [[ "${approved}" == "no" ]]; then
        unapproved_count=$((unapproved_count + 1))
        echo "::warning file=${range_sql}::Potentially destructive migration statement without a reviewed exception record at ${line_no}: ${statement}"
    fi
done

rm -f "${range_sql}"

echo >> "${summary}"
if [[ "${unapproved_count}" -gt 0 ]]; then
    echo "**${unapproved_count} unapproved destructive statement(s) found.**" >> "${summary}"
    echo "Add a reviewed exception at \`${exceptions_dir}/<MigrationName>.md\` documenting the" >> "${summary}"
    echo "prior expand release, earliest eligible release, backfill evidence, rollback plan, and" >> "${summary}"
    echo "approving owner, or revise the migration to be expand-only." >> "${summary}"
    if [[ "${enforce}" == "true" ]]; then
        exit 1
    fi
else
    echo "All destructive statements have a matching reviewed exception record." >> "${summary}"
fi
