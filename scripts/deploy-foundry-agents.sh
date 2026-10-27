#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v azd >/dev/null 2>&1; then
    printf '%s\n' "azd is required. Install or update Azure Developer CLI before continuing." >&2
    exit 1
fi

azd_version_output="$(azd version)"
azd_version="$(printf '%s\n' "$azd_version_output" |
    sed -nE 's/.*version ([0-9]+\.[0-9]+\.[0-9]+).*/\1/p' |
    head -n 1)"
minimum_azd_version="1.36.0"
if [[ -z "$azd_version" ]] ||
    [[ "$(printf '%s\n' "$minimum_azd_version" "$azd_version" | sort -V | head -n 1)" != "$minimum_azd_version" ]]; then
    printf 'Found azd %s; this Foundry project requires azd %s or later.\n' \
        "${azd_version:-unknown}" "$minimum_azd_version" >&2
    exit 1
fi

if [[ -z "${AGENT_TOOLS_BASE_URL:-}" ]] ||
    [[ ! "$AGENT_TOOLS_BASE_URL" =~ ^https://[^[:space:]]+$ ]]; then
    printf '%s\n' "Set AGENT_TOOLS_BASE_URL to the public HTTPS URL of the deployed backend." >&2
    exit 1
fi

if [[ -z "${AGENT_TOOLS_API_KEY:-}" || ${#AGENT_TOOLS_API_KEY} -lt 32 ]]; then
    printf '%s\n' "Set AGENT_TOOLS_API_KEY to a secret of at least 32 characters." >&2
    exit 1
fi

printf '%s\n' \
    "This will provision or update Foundry resources and may create billable Azure resources." \
    "Confirm the selected azd environment and subscription before continuing."
read -r -p "Type DEPLOY-RIO-CLARO-FOUNDRY to continue: " confirmation
if [[ "$confirmation" != "DEPLOY-RIO-CLARO-FOUNDRY" ]]; then
    printf '%s\n' "Deployment cancelled."
    exit 1
fi

printf '%s\n' "Running azd up: provision the Foundry project/model and deploy its connection and agents."
azd up

cat <<'EOF'
Deployment completed. Configure the backend App Service using the Foundry project endpoint,
the deployed agent names and versions, and the same AgentTools API key. Keep the key in
App Settings or a secret store; do not put it in source control. Confirm the agent versions
with `azd ai agent show business-agent` and `azd ai agent show invoice-agent`.
EOF
