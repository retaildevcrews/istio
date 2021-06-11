FROM ghcr.io/retaildevcrews/kind:latest

# install rust
RUN sudo apt-get install -y build-essential && \
curl https://sh.rustup.rs > rust.sh && \
chmod +x rust.sh && \
./rust.sh -y && \
rm rust.sh && \
$HOME/.cargo/bin/cargo install cargo-debug
