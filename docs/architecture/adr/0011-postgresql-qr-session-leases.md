# ADR-0011: leases PostgreSQL para sessões QR distribuídas

**Status:** Aceito — 2026-08-30

## Contexto

Uma sessão Baileys mantém socket, credenciais e estado em memória. Réplicas da
ponte QR atrás do mesmo endereço podiam abrir a mesma sessão e gravar os mesmos
arquivos, causando desconexões, perda de estado e envio para a réplica errada.

## Decisão

Cada sessão identificada por tenant e linha QR possui um lease exclusivo e com
expiração no PostgreSQL. A ponte só abre ou usa o socket depois de adquirir ou
renovar o próprio lease. A instância anuncia seu endereço interno estável; uma
réplica sem ownership recebe conflito com o endereço do dono. API e worker
seguem esse endereço uma vez para a operação.

O lease é renovado antes do vencimento e a ponte encerra o socket se perder o
ownership. Credenciais seguem cifradas no `ISecretStore`; volumes locais não
são fonte de verdade entre instâncias.

## Consequências

- Não introduz Redis, broker, microsserviço ou Kubernetes.
- Cada réplica precisa de `WHATSAPP_WEB_INSTANCE_URL` único e alcançável pela
  API e pelo worker.
- Failover ocorre após expiração do lease e pode causar uma reconexão Baileys.
- A operação deve monitorar leases vencidos, conflito de ownership e falha de
  renovação.
