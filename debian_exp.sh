export RUSTFLAGS="-C link-arg=-fuse-ld=lld"
cargo build --target x86_64-unknown-linux-musl --release; 
#cargo build --release; 
fossil uv add target/x86_64-unknown-linux-musl/release/accept-connect --as a-c-x86_64-linux-musl-exp; 
fossil uv add target/x86_64-unknown-linux-musl/release/offer-listen --as o-l-x86_64-linux-musl-exp; 
#fossil uv add target/release/accept-connect --as a-c-x86_64-linux-glibc-bookworm; 
#fossil uv add target/release/offer-listen --as o-l-x86_64-linux-glibc-bookworm
export RUSTFLAGS="-C link-arg=-fuse-ld=lld"
