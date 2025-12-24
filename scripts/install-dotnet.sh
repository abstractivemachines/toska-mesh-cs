#!/usr/bin/env bash
set -euo pipefail

DOTNET_INSTALL_DIR=${DOTNET_INSTALL_DIR:-"${HOME}/.dotnet"}
DOTNET_CHANNEL=${DOTNET_CHANNEL:-"8.0"}

has_dotnet_8() {
  if command -v dotnet >/dev/null 2>&1; then
    if dotnet --list-sdks | awk '{print $1}' | grep -qE '^8\\.'; then
      return 0
    fi
  fi

  if [[ -x "${DOTNET_INSTALL_DIR}/dotnet" ]]; then
    if "${DOTNET_INSTALL_DIR}/dotnet" --list-sdks | awk '{print $1}' | grep -qE '^8\\.'; then
      return 0
    fi
  fi

  return 1
}

if has_dotnet_8; then
  echo ".NET SDK 8.x already installed."
  exit 0
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required to download dotnet-install.sh." >&2
  exit 1
fi

tmp_script="$(mktemp)"
curl -fsSL "https://dot.net/v1/dotnet-install.sh" -o "${tmp_script}"
bash "${tmp_script}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_INSTALL_DIR}"
rm -f "${tmp_script}"

cat <<EOF
.NET SDK 8.x installed to ${DOTNET_INSTALL_DIR}
Add to your shell profile:
  export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
  export PATH="\${DOTNET_ROOT}:\${PATH}"
EOF
