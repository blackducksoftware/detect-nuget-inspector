FROM releng/base-gitlab-runner:jdk17-python3.11-git

ARG PYTHON_PYPI_URL

ARG OUTPUT_FILE_NAME

RUN mkdir signing \
    && cd signing

RUN python3 -m venv ./environment \
    && . ./environment/bin/activate \
    && pip3 install --upgrade pip ${PYTHON_PYPI_URL} \
    && pip3 install SIG-macOS-codesigning ${PYTHON_PYPI_URL}

RUN echo "Start signing the zip project"

COPY /entrypoint.sh /signing

RUN chmod +x /signing/entrypoint.sh

ENTRYPOINT ["/bin/bash","/signing/entrypoint.sh"]