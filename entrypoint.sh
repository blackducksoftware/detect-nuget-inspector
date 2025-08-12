set -e

python3 -m venv ./environment
. ./environment/bin/activate
pip3 install --upgrade pip
pip3 install SIG-macOS-codesigning "$1"

echo "Start signing the zip project"

signing-client --debug end2end "$2"

wait
exec "@"