# v2rayN-Plus

A fork of [2dust/v2rayN](https://github.com/2dust/v2rayN) with the features below added. Everything else works as in v2rayN.

Download: [Releases](https://github.com/e3ang/v2rayN-Plus/releases) (Windows x64, `v2rayN-windows-64.zip`, cores included).

## Added features

### SSH tunnel
Servers > Add [SSH]. Enter the host, port, user, and then either a password or a private key (pasted, or a path such as `~/.ssh/id_ed25519`). Optional settings: host key pinning, host key algorithms, client version. Runs on the sing-box core, and works inside proxy chains.

Some networks freeze SSH connections for tens of seconds. While an SSH server is active, v2rayN checks the tunnel every 5 seconds. If two checks in a row fail, it opens a fresh SSH session, and keeps retrying every 10 seconds until the path opens. It can be switched off in Settings > Auto switch.

### Apps tab (per-app traffic)
Lists every app that goes through v2rayN with its connections, upload, download and live speed. Right-click an app or use the toolbar to:
- block its internet
- send it direct (bypass the proxy)
- always send it through the proxy
- remove the rule

Rules are saved as `AppControl` process rules at the top of the active routing set, and the core reloads automatically. Requires the sing-box core: an SSH server, any server set to sing-box, or TUN mode. In system-proxy mode, only apps that use the system proxy are visible.

### Auto switch
Settings > Auto switch. Every minute the active server is checked. If it stops responding, all servers are tested by real delay right away and the fastest working one becomes active. A full test also runs every N minutes (default 30). The server only changes if the new one is at least X% faster (default 30%). This can be limited to the selected subscription.

### Subscription usage and expiry
Reads the `subscription-userinfo` header sent by 3x-ui, Marzban, Remnawave and similar panels. Shows used / total traffic and days left next to each subscription and in the subscription list.

### HWID header
Edit subscription > "Send device ID (HWID)". Sends `x-hwid`, `x-device-os`, `x-ver-os` and `x-device-model`, the same headers Happ sends, for panels that limit the number of devices. The ID is a hash of the OS machine ID: stable, and it does not expose the machine ID itself. Headers you set manually take priority.

### AmneziaWG
Servers > Add [AmneziaWG / WireGuard .conf]. Browse to a `.conf` exported by AmneziaVPN or wg-quick. Xray and sing-box do not support AmneziaWG, so the file is converted into a mihomo profile, keeping all `Jc`, `Jmin`, `Jmax`, `S1`-`S4`, `H1`-`H4` and `I1`-`I5` values. The Apps tab rules and auto switch do not apply to these profiles.

### Copy / paste
- SSH servers share as `ssh://user:pass@host:port?pk=<key>&hk=<host key>#name`. These links paste back in through the usual import from clipboard. Hiddify-style `pk` / `hk` links and the long sing-box parameter names are also accepted.
- Pasting an AmneziaWG `.conf` text imports it as AmneziaWG (mihomo), not as plain WireGuard. Exporting the share link of that profile gives the original `.conf` back.

### Updates
In-app updates of v2rayN come from this repository's releases, so the official build never replaces this one. Core updates (Xray, sing-box, mihomo) still come from upstream.

## Build

```
dotnet publish v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true
```

Requires the .NET 10 SDK. Pushing a tag such as `7.25.7` builds the Windows zip and publishes the release (`.github/workflows/release.yml`).

## License

GPL-3.0, same as v2rayN. See [LICENSE](LICENSE).
