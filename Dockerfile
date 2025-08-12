FROM releng/base-gitlab-runner:jdk17-python3.11-git

ARG SIGSC_USER
ENV SIGSC_USER=$SIGSC_USER

ARG SIGSC_PASSWORD
ENV SIGSC_PASSWORD=$SIGSC_PASSWORD

ARG PYTHON_PYPI

RUN mkdir signing \
    && cd signing

COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh

RUN python3 -m venv ./environment
RUN . ./environment/bin/activate
RUN pip3 install --upgrade pip ${PYTHON_PYPI}
RUN pip3 install ${PYTHON_PYPI} sig-macos-codesigning

ENTRYPOINT ["/bin/bash","/entrypoint.sh"]