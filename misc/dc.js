dc.onmessage = e => log(`Message: '${dc.label}' receives '${(new TextDecoder()).decode(new Uint8Array(e.data))}'`)