exec python3 -m venv ./environment \
    && . ./environment/bin/activate \
    && pip3 install --upgrade pip ${PYTHON_PYPI_URL} \
    && pip3 install SIG-macOS-codesigning ${PYTHON_PYPI_URL}

exec echo "Start signing the zip project"

exec signing-client --debug end2end "$OUTPUT_FILE_NAME" "@"