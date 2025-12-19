#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
ROOT_DIR=$(cd -- "${SCRIPT_DIR}/.." && pwd)
WHEELHOUSE=${1:-"${ROOT_DIR}/local-packages"}
INSTALL_ROOT=${2:-"${HOME}/Applications/toska-mesh-cli"}
PYTHON_BIN=${PYTHON_BIN:-python3}

if [[ -n "${VIRTUAL_ENV:-}" ]]; then
  echo "Please deactivate the virtual environment before running this installer." >&2
  exit 1
fi

if ! ${PYTHON_BIN} -m build --version >/dev/null 2>&1; then
  echo "Missing python build module." >&2
  echo "Install it via your package manager (e.g., 'sudo pacman -S python-build') or pip:" >&2
  echo "  ${PYTHON_BIN} -m pip install --user -U build --break-system-packages" >&2
  exit 1
fi
mkdir -p "${WHEELHOUSE}"
mkdir -p "${INSTALL_ROOT}"
${PYTHON_BIN} -m build --no-isolation --wheel --outdir "${WHEELHOUSE}" "${ROOT_DIR}"
${PYTHON_BIN} -m pip install --no-index --find-links "${WHEELHOUSE}" --prefix "${INSTALL_ROOT}" --upgrade --break-system-packages toska-mesh-cli

export INSTALL_ROOT
mapfile -t LIB_PATHS < <(${PYTHON_BIN} - <<'PY'
import os
import sysconfig

base = os.environ["INSTALL_ROOT"]
paths = {"base": base, "platbase": base}
purelib = sysconfig.get_path("purelib", vars=paths)
platlib = sysconfig.get_path("platlib", vars=paths)
print(purelib)
print(platlib)
PY
)
PURELIB="${LIB_PATHS[0]}"
PLATLIB="${LIB_PATHS[1]}"

WRAPPER_PATH="${HOME}/Applications/toska"
mkdir -p "$(dirname "${WRAPPER_PATH}")"
cat > "${WRAPPER_PATH}" <<EOF
#!/usr/bin/env bash
EXTRA_PYTHONPATH="\${PYTHONPATH:-}"
PYTHONPATH="${PURELIB}"
if [[ "${PLATLIB}" != "${PURELIB}" ]]; then
  PYTHONPATH="\${PYTHONPATH}:${PLATLIB}"
fi
if [[ -n "\${EXTRA_PYTHONPATH}" ]]; then
  PYTHONPATH="\${PYTHONPATH}:\${EXTRA_PYTHONPATH}"
fi
export PYTHONPATH
exec "${PYTHON_BIN}" -m toska_mesh_cli "\$@"
EOF
chmod +x "${WRAPPER_PATH}"

echo "Installed toska-mesh-cli to ${INSTALL_ROOT}"
echo "Wrapper created at ${WRAPPER_PATH}"
