#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v azd >/dev/null 2>&1; then
    printf '%s\n' "azd is required to clean up the selected environment." >&2
    exit 1
fi

if [[ -z "${RIO_CLARO_AZD_ENVIRONMENT:-}" ]]; then
    printf '%s\n' "Set RIO_CLARO_AZD_ENVIRONMENT to the exact azd environment to remove." >&2
    exit 1
fi

printf 'This removes Azure resources provisioned by the azd environment "%s".\n' \
    "$RIO_CLARO_AZD_ENVIRONMENT"
printf '%s\n' "Check the target subscription and resource group; this action cannot be undone."
read -r -p "Type DELETE-$RIO_CLARO_AZD_ENVIRONMENT to continue: " confirmation
if [[ "$confirmation" != "DELETE-$RIO_CLARO_AZD_ENVIRONMENT" ]]; then
    printf '%s\n' "Cleanup cancelled."
    exit 1
fi

printf 'Running azd down for environment "%s". No purge flag is used.\n' \
    "$RIO_CLARO_AZD_ENVIRONMENT"
azd down --environment "$RIO_CLARO_AZD_ENVIRONMENT"
