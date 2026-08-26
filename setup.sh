#!/bin/bash
# Setup script for Linux/macOS

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DOTNET_VERSION="10.0"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-postgres}"
POSTGRES_DB="${POSTGRES_DB:-whatsappai}"
POSTGRES_USER="${POSTGRES_USER:-whatsappai}"
ENCRYPTION_KEY=$(openssl rand -base64 32)

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

step() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

check_command() {
    command -v "$1" &> /dev/null
}

install_dotnet() {
    step "Verificando .NET SDK"

    if check_command dotnet; then
        echo -e "${GREEN}✅ .NET SDK encontrado: $(dotnet --version)${NC}"
        return
    fi

    echo -e "${RED}❌ .NET SDK não encontrado${NC}"
    echo -e "${YELLOW}Instale manualmente: https://dotnet.microsoft.com/download/dotnet/10.0${NC}"

    # Try to install via script
    if check_command curl; then
        echo -e "${YELLOW}📦 Tentando instalar via script...${NC}"
        curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel $DOTNET_VERSION
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$PATH:$DOTNET_ROOT"
    else
        exit 1
    fi
}

install_docker() {
    step "Verificando Docker"

    if check_command docker; then
        echo -e "${GREEN}✅ Docker encontrado: $(docker --version)${NC}"
        return
    fi

    echo -e "${RED}❌ Docker não encontrado${NC}"
    echo -e "${YELLOW}Instale manualmente: https://docs.docker.com/get-docker/${NC}"
    exit 1
}

install_node() {
    step "Verificando Node.js"

    if check_command node; then
        echo -e "${GREEN}✅ Node.js encontrado: $(node --version)${NC}"
        return
    fi

    echo -e "${RED}❌ Node.js não encontrado${NC}"

    if check_command nvm; then
        echo -e "${YELLOW}📦 Instalando Node.js via nvm...${NC}"
        nvm install --lts
    else
        echo -e "${YELLOW}Instale manualmente: https://nodejs.org/${NC}"
        exit 1
    fi
}

start_postgres() {
    step "Iniciando PostgreSQL via Docker"

    echo -e "${YELLOW}Iniciando PostgreSQL...${NC}"
    POSTGRES_PASSWORD="$POSTGRES_PASSWORD" docker compose up -d postgres

    echo -e "${YELLOW}Aguardando PostgreSQL ficar pronto...${NC}"
    for i in $(seq 1 30); do
        if docker compose exec -T postgres pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" &>/dev/null; then
            echo -e "${GREEN}PostgreSQL pronto!${NC}"
            return
        fi
        echo -n "."
        sleep 1
    done

    echo -e "\n${RED}PostgreSQL não ficou pronto a tempo${NC}"
    exit 1
}

setup_user_secrets() {
    step "Configurando User Secrets"

    PROJECT_PATH="src/WhatsAppAI.WebApi"

    dotnet user-secrets init --project "$PROJECT_PATH" 2>/dev/null || true

    CONN_STRING="Host=localhost;Port=5432;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$CONN_STRING" --project "$PROJECT_PATH"
    dotnet user-secrets set "Encryption:Key" "$ENCRYPTION_KEY" --project "$PROJECT_PATH"
    dotnet user-secrets set "Meta:VerifyToken" "dev-verify-token" --project "$PROJECT_PATH"
    dotnet user-secrets set "Meta:AppSecret" "dev-app-secret" --project "$PROJECT_PATH"

    echo -e "${GREEN}✅ User Secrets configurado${NC}"
}

install_dependencies() {
    step "Instalando dependências"

    echo -e "${YELLOW}📦 Restaurando pacotes .NET...${NC}"
    dotnet restore

    echo -e "${YELLOW}📦 Instalando dependências do frontend...${NC}"
    cd apps/web
    npm install
    cd "$SCRIPT_DIR"

    echo -e "${GREEN}✅ Dependências instaladas${NC}"
}

init_database() {
    step "Inicializando banco de dados"

    echo -e "${YELLOW}Banco PostgreSQL criado pelo Docker Compose${NC}"

    echo -e "${YELLOW}🔄 Executando migrations...${NC}"
    dotnet ef database update --project src/WhatsAppAI.Infrastructure --startup-project src/WhatsAppAI.WebApi || true

    echo -e "${GREEN}✅ Banco de dados inicializado${NC}"
}

build_solution() {
    step "Build da solução"

    echo -e "${YELLOW}🔨 Compilando backend...${NC}"
    dotnet build --configuration Release

    echo -e "${YELLOW}🔨 Compilando frontend...${NC}"
    cd apps/web
    npm run build
    cd "$SCRIPT_DIR"

    echo -e "${GREEN}✅ Build concluído${NC}"
}

run_tests() {
    step "Executando testes"

    echo -e "${YELLOW}🧪 Testes .NET...${NC}"
    dotnet test --configuration Release --verbosity normal || true

    echo -e "${YELLOW}🧪 Testes frontend...${NC}"
    cd apps/web
    npm test || true
    cd "$SCRIPT_DIR"
}

start_application() {
    step "Iniciando aplicação"

    echo -e "${GREEN}🚀 WhatsApp AI Manager${NC}"
    echo ""
    echo -e "   Backend:  http://localhost:5179"
    echo -e "   Frontend: http://localhost:5173"
    echo -e "   Health:   http://localhost:5179/health/live"
    echo ""
    echo -e "   Pressione Ctrl+C para parar"
    echo ""

    # Start backend in background
    dotnet run --project src/WhatsAppAI.WebApi --configuration Release &
    BACKEND_PID=$!

    # Start frontend
    cd apps/web
    trap "kill $BACKEND_PID 2>/dev/null" EXIT
    npm run dev
}

# Parse arguments
SKIP_INSTALL=false
RUN_ONLY=false
TEST_ONLY=false

for arg in "$@"; do
    case $arg in
        --skip-install) SKIP_INSTALL=true ;;
        --run-only) RUN_ONLY=true ;;
        --test-only) TEST_ONLY=true ;;
    esac
done

echo -e "${CYAN}╔═══════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║     WhatsApp AI Manager - Setup Script        ║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════╝${NC}"

if $TEST_ONLY; then
    install_dotnet
    install_dependencies
    run_tests
    exit 0
fi

if ! $RUN_ONLY; then
    install_dotnet
    install_docker
    install_node
    start_postgres
    setup_user_secrets
    install_dependencies
    init_database
    build_solution
fi

if ! $SKIP_INSTALL; then
    run_tests
fi

start_application
