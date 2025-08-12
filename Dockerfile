FROM releng/base-gitlab-runner:jdk17-python3.11-git

ARG SIGSC_USER
ENV SIGSC_USER=$SIGSC_USER

ARG SIGSC_PASSWORD
ENV SIGSC_PASSWORD=$SIGSC_PASSWORD

RUN mkdir signing \
    && cd signing

COPY entrypoint.sh /signing

RUN chmod +x /signing/entrypoint.sh

ENTRYPOINT ["/bin/bash","/signing/entrypoint.sh"]