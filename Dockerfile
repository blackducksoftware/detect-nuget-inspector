FROM releng/base-gitlab-runner:jdk17-python3.11-git

ARG PYTHON_PYPI_URL

ARG OUTPUT_FILE_NAME

RUN mkdir signing \
    && cd signing

COPY /entrypoint.sh /signing

RUN chmod +x /signing/entrypoint.sh

ENTRYPOINT ["/bin/bash","/signing/entrypoint.sh"]