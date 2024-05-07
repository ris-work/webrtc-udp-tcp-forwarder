export RUSTFLAGS="-C link-arg=-fuse-ld=lld"
cargo pgo build -- --target x86_64-unknown-linux-musl --release; 
fossil uv add target/x86_64-unknown-linux-musl/release/accept-connect --as a-c-x86_64-linux-musl-pgo; 
fossil uv add target/x86_64-unknown-linux-musl/release/offer-listen --as o-l-x86_64-linux-musl-pgo; 
#fossil uv add target/release/accept-connect --as a-c-x86_64-linux-glibc-bookworm; 
#fossil uv add target/release/offer-listen --as o-l-x86_64-linux-glibc-bookworm
export RUSTFLAGS="-C link-arg=-fuse-ld=lld"
