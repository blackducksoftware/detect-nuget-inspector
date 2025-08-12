set -e

. ./environment/bin/activate

echo "Start signing the nuget mac zip"

signing-client --debug end2end "$1"