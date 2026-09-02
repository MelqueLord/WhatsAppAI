#!/usr/bin/env bash
# Prepare an Ubuntu 24.04 or 26.04 Hostinger VPS for the WhatsApp AI Platform.
set -Eeuo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Execute como root: sudo bash deploy/hostinger-install.sh"
  exit 1
fi

. /etc/os-release
if [[ "${ID:-}" != "ubuntu" || "${VERSION_ID:-}" != "24.04" && "${VERSION_ID:-}" != "26.04" ]]; then
  echo "Este instalador exige Ubuntu 24.04 ou 26.04; encontrado ${PRETTY_NAME:-sistema desconhecido}." >&2
  exit 1
fi

apt-get update
apt-get install -y ca-certificates curl git ufw certbot
install -m 0755 -d /etc/apt/keyrings
if [[ ! -f /etc/apt/keyrings/docker.asc ]]; then
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
fi

if [[ ! -f /etc/apt/sources.list.d/docker.list ]]; then
  . /etc/os-release
  docker_codename="${UBUNTU_CODENAME:-$VERSION_CODENAME}"
  printf 'deb [arch=%s signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu %s stable\n' \
    "$(dpkg --print-architecture)" "$docker_codename" > /etc/apt/sources.list.d/docker.list
fi

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker

if [[ "${CONFIGURE_UFW:-1}" == "1" ]]; then
  ssh_port="${HOSTINGER_SSH_PORT:-22}"
  ufw allow "${ssh_port}/tcp"
  ufw allow 80/tcp
  ufw allow 443/tcp
  ufw --force enable
fi

echo "Servidor preparado. Portas públicas permitidas: SSH, 80 e 443."
echo "Próximo passo: clone o projeto em /opt/whatsappai e execute deploy/hostinger-deploy.sh."
