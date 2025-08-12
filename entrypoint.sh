set -e

. ./environment/bin/activate

echo "Start signing the zip project"

signing-client --debug end2end "/signing/$1"

wait
exec "@"