# Produção na Hostinger KVM 2

Este runbook usa Docker Compose, PostgreSQL local e um único processo de API
com um processo separado de workers. Os segredos ficam somente no `.env` do
servidor e não devem ser versionados.

## Pré-requisitos

- Use um VPS/KVM com Ubuntu 24.04 e Docker. O plano de hospedagem compartilhada
  não suporta este Compose, workers persistentes, SignalR e a ponte QR.
- Reserve um domínio/subdomínio exclusivo para a plataforma, por exemplo
  `app.seudominio.com`.
- Tenha as credenciais da Meta Cloud API, do provedor de IA e acesso SSH ao VPS.

## Preparação do servidor

1. Aponte o DNS do domínio para o IP do KVM. Crie registros A para `@` e `www`
   somente se ambos forem usados; remova registros A/AAAA/CNAME conflitantes.
2. No firewall do VPS, libere somente SSH (a porta mostrada no painel), HTTP 80
   e HTTPS 443. Não libere 3000, 5000, 5432 ou 3020 para a internet.
3. No projeto, execute o instalador idempotente (Docker Compose, Certbot e
   firewall):

   ```bash
   sudo HOSTINGER_SSH_PORT=22 bash deploy/hostinger-install.sh
   ```

   Se o painel mostrar outra porta SSH, substitua `22`. Para configurar o
   firewall manualmente, use `CONFIGURE_UFW=0`.
4. Clone o repositório em um diretório persistente, por exemplo
   `/opt/whatsappai`, e entre nele.

O DNS pode levar até 24 horas para propagar. Confirme antes de solicitar o
certificado TLS.

## Configuração

```bash
cp deploy/.env.production.example .env
openssl rand -base64 32
chmod 600 .env
```

Preencha o `.env` com valores únicos para banco, `Encryption__Key`, Meta,
`BootstrapAdmin__Email` e `BootstrapAdmin__Password`. Gere ou receba um PFX
exclusivo para o Data Protection, protegido por senha, coloque-o no caminho
indicado por `DATAPROTECTION_CERTIFICATE_FILE` e preencha
`DataProtection__CertificatePassword`. O PFX e os certificados não devem ser
versionados. Configure também `DOMAIN` e mantenha `Persistence__MaxPoolSize`
em 50 inicialmente; ajuste após observar o uso de conexões do PostgreSQL.

Para TLS, crie o diretório e gere um certificado Let's Encrypt depois que o DNS
estiver apontando. Como o Nginx deste projeto redireciona HTTP para HTTPS, faça
o primeiro certificado em modo standalone, antes de iniciar o Nginx:

```bash
mkdir -p deploy/nginx/certs
docker compose --profile production stop nginx 2>/dev/null || true
sudo certbot certonly --standalone -d app.seudominio.com
sudo cp /etc/letsencrypt/live/app.seudominio.com/fullchain.pem deploy/nginx/certs/
sudo cp /etc/letsencrypt/live/app.seudominio.com/privkey.pem deploy/nginx/certs/
sudo chown -R root:root deploy/nginx/certs
sudo chmod 600 deploy/nginx/certs/privkey.pem
```

Configure renovação automática e, após renovar, copie novamente os dois
arquivos para `deploy/nginx/certs/` e execute `docker compose restart nginx`.
Não versione certificados.

Para automatizar a renovação, use o script do projeto:

```bash
sudo LETSENCRYPT_EMAIL=voce@seudominio.com bash deploy/hostinger-renew-certificate.sh app.seudominio.com
```

O Compose cria e compartilha o volume `dataprotection-keys` entre API e worker.
Não remova esse volume durante uma atualização: ele contém as chaves que
validam cookies e tokens antiforgery existentes. Faça backup dele junto com o
PFX correspondente.

## Escala da ponte QR

Cada instância da ponte QR precisa de um endereço interno estável e exclusivo,
configurado em `WHATSAPP_WEB_INSTANCE_URL`. Não use `docker compose --scale`
com a mesma URL para todas as réplicas. Para cada instância adicional, crie um
serviço com nome/DNS e volume de sessões próprios; a ponte usa um lease no
PostgreSQL para garantir que somente uma instância controla cada linha QR.

O Compose declara a segunda ponte como `whatsapp-web-2`, isolada no volume
`whatsapp-web-sessions-2`. Para ativá-la, configure
`WHATSAPP_WEB_INSTANCE_2_ID=whatsapp-web-2` e
`WHATSAPP_WEB_INSTANCE_2_URL=http://whatsapp-web-2:3020` no `.env` e inicie
o perfil adicional:

```bash
docker compose --profile production --profile qr-scale up -d
docker compose ps whatsapp-web whatsapp-web-2
```

API e worker entram primeiro por `whatsapp-web`; se a sessão pertencer à
segunda ponte, seguem uma única vez o endereço retornado pelo lease. Não
publique portas das pontes QR nem compartilhe seus volumes. Para uma terceira
instância, replique o padrão com novo nome DNS, ID, URL e volume, em vez de
escalar o mesmo serviço.

## Primeiro deploy

```bash
chmod +x deploy/hostinger-*.sh deploy/backup.sh deploy/restore.sh
./deploy/hostinger-deploy.sh
```

Antes de `docker compose config`, confira se o `.env` contém
`DATAPROTECTION_CERTIFICATE_FILE=./deploy/secrets/dataprotection.pfx` e se esse
PFX existe. O primeiro `docker compose build` pode levar alguns minutos.

Depois do deploy, valide: login do administrador, criação/ativação de um tenant,
`/api/auth/me`, uma conexão SignalR, o webhook de verificação da Meta e uma
mensagem de teste. Faça o teste de QR somente se a ponte QR estiver habilitada.

O serviço `migrate` é executado uma vez e não permanece em loop. A API não
inicia workers; o serviço `worker` é o único responsável pelo processamento
assíncrono.

## Operação e rollback

- Faça backup diário com `./deploy/backup.sh` e mantenha cópias fora do KVM.
- O volume `whatsapp-web-sessions` mantém as sessões QR durante recriações do
  container; inclua esse volume na rotina de backup antes de remover o stack.
- Se o perfil `qr-scale` estiver ativo, inclua também
  `whatsapp-web-sessions-2`; esses volumes nunca devem ser compartilhados.
- O volume `dataprotection-keys` mantém o key ring compartilhado entre API e
  worker; inclua-o na rotina de backup e preserve também o PFX usado para
  cifrá-lo.
- Antes de atualizar, execute o backup e registre a imagem/commit atual.
- Para atualizar: faça backup, execute `git pull` e rode novamente
  `./deploy/hostinger-deploy.sh`; o script valida o ambiente, recompila,
  aplica migration e executa os health checks.
- Em falha, volte ao commit anterior, reconstrua as imagens e reinicie o
  perfil. Restaure o banco somente após confirmar o arquivo de backup.
- Monitore `docker compose logs -f api worker` e os endpoints de health.

## Backup diário

Crie uma cópia externa do banco e dos volumes de sessão/chaves. Para o backup
diário do banco, ajuste o caminho no cron do servidor:

```bash
sudo mkdir -p /var/backups/whatsappai
sudo crontab -e
0 2 * * * cd /opt/whatsappai && ./deploy/backup.sh >> /var/log/whatsappai-backup.log 2>&1
17 3 1 * * cd /opt/whatsappai && LETSENCRYPT_EMAIL=voce@seudominio.com ./deploy/hostinger-renew-certificate.sh app.seudominio.com >> /var/log/whatsappai-certbot.log 2>&1
```

Copie periodicamente os backups para outro armazenamento. Teste uma restauração
em staging antes de considerar o procedimento confiável.
