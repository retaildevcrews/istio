FROM ghcr.io/retaildevcrews/kind:beta

RUN export DEBIAN_FRONTEND=noninteractive && \
    apt-get update

RUN apt-get install -y pkg-config libssl-dev
RUN apt-get install -y python
RUN apt-get install -y clang

RUN apt-get install -y cmake

# install rust
ENV RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/usr/local/cargo \
    PATH=/usr/local/cargo/bin:$PATH \
    RUST_VERSION=1.52.1 \
    USER=vscode

RUN set -eux; \
    dpkgArch="$(dpkg --print-architecture)"; \
    case "${dpkgArch##*-}" in \
        amd64) rustArch='x86_64-unknown-linux-gnu'; rustupSha256='fb3a7425e3f10d51f0480ac3cdb3e725977955b2ba21c9bdac35309563b115e8' ;; \
        armhf) rustArch='armv7-unknown-linux-gnueabihf'; rustupSha256='f263e170da938888601a8e0bc822f8b40664ab067b390cf6c4fdb1d7c2d844e7' ;; \
        arm64) rustArch='aarch64-unknown-linux-gnu'; rustupSha256='de1dddd8213644cba48803118c7c16387838b4d53b901059e01729387679dd2a' ;; \
        i386) rustArch='i686-unknown-linux-gnu'; rustupSha256='66c03055119cecdfc20828c95429212ae5051372513f148342758bb5d0130997' ;; \
        *) echo >&2 "unsupported architecture: ${dpkgArch}"; exit 1 ;; \
    esac; \
    url="https://static.rust-lang.org/rustup/archive/1.24.1/${rustArch}/rustup-init"; \
    wget "$url"; \
    echo "${rustupSha256} *rustup-init" | sha256sum -c -; \
    chmod +x rustup-init; \
    ./rustup-init -y --no-modify-path --profile minimal --default-toolchain $RUST_VERSION --default-host ${rustArch}; \
    rm rustup-init; \
    chmod -R a+w $RUSTUP_HOME $CARGO_HOME;

RUN apt-get upgrade -y

RUN apt-get autoremove -y && \
    apt-get clean -y

WORKDIR /home/vscode
USER vscode

# install cargo debug
RUN cargo install cargo-debug
RUN rustup update
RUN rustup component add rust-analysis && \
    rustup component add rust-src && \
    rustup component add rls
RUN rustup target add wasm32-unknown-unknown

#RUN curl -sL https://run.solo.io/wasme/install | sh

# install webv
RUN dotnet tool install -g webvalidate
