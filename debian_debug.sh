export RUSTFLAGS="-C link-arg=-fuse-ld=lld"
cargo build --target x86_64-unknown-linux-musl ; 
cargo build ; 
fossil uv add target/x86_64-unknown-linux-musl/debug/accept-connect --as a-c-x86_64-linux-musl-d; 
fossil uv add target/x86_64-unknown-linux-musl/debug/offer-listen --as o-l-x86_64-linux-musl-d; 
fossil uv add target/debug/accept-connect --as a-c-x86_64-linux-glibc-bookworm-d; 
fossil uv add target/debug/offer-listen --as o-l-x86_64-linux-glibc-bookworm-d
