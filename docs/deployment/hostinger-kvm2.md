# Produção no Hostinger KVM 2

Este runbook usa Docker Compose, PostgreSQL local e um único processo de API
com um processo separado de workers. Os segredos ficam somente no `.env` do
servidor e não devem ser versionados.

## Preparação do servidor

1. Aponte o DNS do domínio para o IP do KVM.
2. Libere somente SSH, HTTP e HTTPS no firewall (portas 22, 80 e 443).
3. Instale Docker Engine e o plugin Docker Compose.
4. Clone o repositório em um diretório persistente, por exemplo
   `/opt/whatsappai`, e entre nele.

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

Para TLS, coloque `fullchain.pem` e `privkey.pem` em
`deploy/nginx/certs/` antes de iniciar o perfil `production`.

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

## Primeiro deploy

```bash
docker compose config
docker compose build
docker compose up -d postgres
docker compose ps postgres
docker compose run --rm migrate
docker compose --profile production up -d
docker compose ps
curl -fsS https://SEU_DOMINIO/health/live
curl -fsS https://SEU_DOMINIO/health/ready
```

O serviço `migrate` é executado uma vez e não permanece em loop. A API não
inicia workers; o serviço `worker` é o único responsável pelo processamento
assíncrono.

## Operação e rollback

- Faça backup diário com `./deploy/backup.sh` e mantenha cópias fora do KVM.
- O volume `whatsapp-web-sessions` mantém as sessões QR durante recriações do
  container; inclua esse volume na rotina de backup antes de remover o stack.
- O volume `dataprotection-keys` mantém o key ring compartilhado entre API e
  worker; inclua-o na rotina de backup e preserve também o PFX usado para
  cifrá-lo.
- Antes de atualizar, execute o backup e registre a imagem/commit atual.
- Para atualizar: `git pull`, `docker compose build`, execute `migrate` e
  depois `docker compose --profile production up -d`.
- Em falha, volte ao commit anterior, reconstrua as imagens e reinicie o
  perfil. Restaure o banco somente após confirmar o arquivo de backup.
- Monitore `docker compose logs -f api worker` e os endpoints de health.
