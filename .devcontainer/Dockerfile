FROM ghcr.io/retaildevcrews/kind:latest

# install rust
RUN apt-get install -y build-essential python cmake pkg-config libssl-dev clang

ENV RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/usr/local/cargo \
    PATH=/usr/local/cargo/bin:$PATH \
    RUST_VERSION=1.52.1 \
    ISTIO_VERSION=1.10.1 \
    ISTIO_HOME=/usr/local/istio \
    USER=akdc

WORKDIR /home/akdc
USER akdc

RUN echo "export ISTIO_VERSION=$ISTIO_VERSION" >> /home/akdc/.bashrc && \
    echo "export ISTIO_HOME=$ISTIO_HOME" >> /home/akdc/.bashrc && \
    echo 'export PATH=$HOME/.wasme/bin:$ISTIO_HOME/bin:$PATH' >> /home/akdc/.bashrc

# install cargo debug
RUN cargo install cargo-debug && \
    rustup update && \
    rustup target add wasm32-unknown-unknown

RUN curl -sL https://run.solo.io/wasme/install | sh

# install webv
RUN dotnet tool install -g webvalidate

#install istioctl
RUN curl -L https://istio.io/downloadIstio | sh -  && \
    sudo mv istio-$ISTIO_VERSION $ISTIO_HOME

RUN rustup component add rust-analysis && \
    rustup component add rust-src && \
    rustup component add rls
