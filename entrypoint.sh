set -e

PYTHON_PYPI_URL="$1"
OUTPUT_FILE_NAME="$2"

python3 -m venv ./environment
. ./environment/bin/activate
pip3 install --upgrade pip "$PYTHON_PYPI_URL"
pip3 install SIG-macOS-codesigning "$PYTHON_PYPI_URL"

echo "Start signing the zip project"

signing-client --debug end2end "$OUTPUT_FILE_NAME"

wait
exec "@"