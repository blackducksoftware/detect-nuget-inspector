set -e

. ./environment/bin/activate

echo "Start signing the zip project"

signing-client --debug end2end "$1"

wait
exec "@"