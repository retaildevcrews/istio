FROM ghcr.io/cse-labs/k3d-wasm:latest

ARG USERNAME=vscode

# install fluent-bit
RUN curl https://packages.fluentbit.io/fluentbit.key | gpg --dearmor > /usr/share/keyrings/fluentbit-keyring.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/fluentbit-keyring.gpg] https://packages.fluentbit.io/ubuntu/focal focal main" > /etc/apt/sources.list.d/fluent-bit.list && \
    apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y fluent-bit && \
    echo 'export PATH=$PATH:/opt/fluent-bit/bin' >> /home/$USERNAME/.zshrc

# add your commands here
