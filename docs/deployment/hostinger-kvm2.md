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
`BootstrapAdmin__Email` e `BootstrapAdmin__Password`. Configure também
`DOMAIN` e mantenha `Persistence__MaxPoolSize` em 50 inicialmente; ajuste após
observar o uso de conexões do PostgreSQL.

Para TLS, coloque `fullchain.pem` e `privkey.pem` em
`deploy/nginx/certs/` antes de iniciar o perfil `production`.

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
- Antes de atualizar, execute o backup e registre a imagem/commit atual.
- Para atualizar: `git pull`, `docker compose build`, execute `migrate` e
  depois `docker compose --profile production up -d`.
- Em falha, volte ao commit anterior, reconstrua as imagens e reinicie o
  perfil. Restaure o banco somente após confirmar o arquivo de backup.
- Monitore `docker compose logs -f api worker` e os endpoints de health.
