cargo build --release
fossil uv add .\target\release\offer-listen.exe --as o-l.exe
fossil uv add .\target\release\accept-connect.exe --as a-c.exe
fossil uv add .\target\release\offer-listen-brutal.exe --as o-l-brutal.exe
fossil uv add .\target\release\accept-connect-brutal.exe --as a-c-brutal.exe
