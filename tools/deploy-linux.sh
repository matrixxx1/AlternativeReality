#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET_BIN:-}"
if [[ -z "$dotnet_bin" ]]; then
    dotnet_bin="$(command -v dotnet || true)"
fi
if [[ -z "$dotnet_bin" && -x "$repo_dir/../../.toolchains/dotnet8/dotnet" ]]; then
    dotnet_bin="$repo_dir/../../.toolchains/dotnet8/dotnet"
fi
if [[ -z "$dotnet_bin" || ! -x "$dotnet_bin" ]]; then
    echo "Install the .NET 8 SDK or set DOTNET_BIN to its executable." >&2
    exit 1
fi
dotnet_bin="$(readlink -f "$dotnet_bin")"

install_dir="$HOME/.local/opt/AlternativeReality"
data_dir="$HOME/.local/share/AlternativeReality/data"
unit_dir="$HOME/.config/systemd/user"
unit_file="$unit_dir/alternative-reality.service"
mkdir -p "$(dirname "$install_dir")" "$data_dir" "$unit_dir"
staging_dir="$(mktemp -d "$(dirname "$install_dir")/.AlternativeReality.XXXXXXXX")"
trap 'rm -rf "$staging_dir"' EXIT

cd "$repo_dir"
"$dotnet_bin" publish src/AlternateEarth.Server/AlternateEarth.Server.csproj \
    -c Release -p:UseAppHost=false -o "$staging_dir"

was_running=false
if systemctl --user is-active --quiet alternative-reality.service; then
    was_running=true
    systemctl --user stop alternative-reality.service
fi
backup_dir="${install_dir}.previous"
rm -rf "$backup_dir"
if [[ -d "$install_dir" ]]; then
    mv "$install_dir" "$backup_dir"
fi
mv "$staging_dir" "$install_dir"

cat > "$unit_file" <<EOF
[Unit]
Description=AlternativeReality game server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=$install_dir
ExecStart=$dotnet_bin $install_dir/AlternateEarth.Server.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Server__Urls=http://0.0.0.0:5080
Environment=Server__DataDirectory=$data_dir
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
if ! systemctl --user enable --now alternative-reality.service; then
    systemctl --user stop alternative-reality.service || true
    rm -rf "$install_dir"
    if [[ -d "$backup_dir" ]]; then mv "$backup_dir" "$install_dir"; fi
    if [[ "$was_running" == true ]]; then systemctl --user start alternative-reality.service; fi
    exit 1
fi

# Tailscale Serve keeps the TLS certificate and renewal outside the game process.
# This adds port 8443 without changing any existing Serve listener, including 443.
if command -v tailscale >/dev/null 2>&1; then
    tailscale serve --bg --https=8443 http://127.0.0.1:5080
fi

echo "AlternativeReality is installed at $install_dir"
echo "Persistent data: $data_dir"
echo "Service: systemctl --user status alternative-reality.service"
echo "Open: http://$(hostname -I | awk '{print $1}'):5080"
if command -v tailscale >/dev/null 2>&1; then
    echo "HTTPS: see tailscale serve status (tailnet only, port 8443)"
fi
