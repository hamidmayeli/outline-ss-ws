client config template the result of `ssconf`: 

```json
{
  "transport": {
    "$type": "tcpudp",
    "tcp": {
      "$type": "shadowsocks",
      "endpoint": {
        "$type": "websocket",
        "url": "wss://your-domain.com/your-tcp-path"
      },
      "cipher": "chacha20-ietf-poly1305",
      "secret": "user-secret-here"
    },
    "udp": {
      "$type": "shadowsocks",
      "endpoint": {
        "$type": "websocket",
        "url": "wss://your-domain.com/your-udp-path"
      },
      "cipher": "chacha20-ietf-poly1305",
      "secret": "user-secret-here"
    }
  }
}
```
or
```yaml
transport:
    type: tcpudp
    tcp: 
      $type: shadowsocks
      endpoint:
        $type: websocket
        url: wss://your-domain.com/your-tcp-path
      cipher: chacha20-ietf-poly1305
      secret: user-secret-here
    
    udp:
      $type: shadowsocks
      endpoint:
        $type: websocket
        url: wss://your-domain.com/your-udp-path
      cipher: chacha20-ietf-poly1305
      secret: user-secret-here
```

> YAML is preferred
