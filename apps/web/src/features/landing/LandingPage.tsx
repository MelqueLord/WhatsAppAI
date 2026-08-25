import { useState } from "react";
import "./LandingPage.css";
import atenzLogo from "../../assets/atenz-logo-a.png";

type IconProps = {
  size?: number;
};

function WhatsAppIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="M20.5 11.7a8.5 8.5 0 0 1-12.6 7.45L3 20.5l1.3-4.75A8.5 8.5 0 1 1 20.5 11.7Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M8.2 7.8c.25-.55.52-.56.77-.57h.66c.2 0 .4.05.55.4l.75 1.8c.1.28.08.5-.08.72l-.58.72c-.18.2-.15.4-.04.6.65 1.2 1.55 2.2 2.75 2.9.22.13.43.1.6-.08l.88-1.02c.2-.23.44-.28.7-.17l1.72.8c.28.13.43.3.46.5.03.2-.04 1.2-.52 1.76-.48.56-1.3 1.02-2.28 1.02-1.05 0-2.63-.43-4.35-1.85-1.98-1.64-3.2-3.62-3.57-5.03-.36-1.4.12-2.14.57-2.5Z"
        fill="currentColor"
      />
    </svg>
  );
}

function UsersIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"
        stroke="currentColor"
        strokeWidth="1.8"
      />
      <circle cx="9" cy="7" r="4" stroke="currentColor" strokeWidth="1.8" />
      <path
        d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"
        stroke="currentColor"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function ChartIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="M4 20V10M10 20V4M16 20v-7M22 20V7"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}

function BotIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <rect
        x="4"
        y="7"
        width="16"
        height="13"
        rx="3"
        stroke="currentColor"
        strokeWidth="1.8"
      />
      <path
        d="M12 3v4M8 12h.01M16 12h.01M8 16h8"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  );
}

function BrainIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="M9.5 4A3.5 3.5 0 0 0 6 7.5v.7a3.4 3.4 0 0 0-2 3.1c0 1.2.6 2.35 1.55 3A3.5 3.5 0 0 0 9 19h1V5.5A1.5 1.5 0 0 0 8.5 4M14.5 4A3.5 3.5 0 0 1 18 7.5v.7a3.4 3.4 0 0 1 2 3.1c0 1.2-.6 2.35-1.55 3A3.5 3.5 0 0 1 15 19h-1V5.5A1.5 1.5 0 0 1 15.5 4"
        stroke="currentColor"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function TagIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="M20.6 13.4 11 23l-9-9V4h10l8.6 8.6a.57.57 0 0 1 0 .8Z"
        stroke="currentColor"
        strokeWidth="1.8"
      />
      <circle cx="7.5" cy="9.5" r="1.5" fill="currentColor" />
    </svg>
  );
}

function PipelineIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <circle cx="5" cy="18" r="2" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="12" cy="12" r="2" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="19" cy="6" r="2" stroke="currentColor" strokeWidth="1.8" />
      <path d="m7 16 3.5-2.8M13.5 10.8 17 8" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

function ZapIcon({ size = 24 }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <path
        d="m13 2-9 12h8l-1 8 9-12h-8l1-8Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
      <path
        d="m5 12 4 4L19 6"
        stroke="currentColor"
        strokeWidth="2.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function ArrowIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
      <path
        d="M5 12h14M13 6l6 6-6 6"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

const features = [
  {
    icon: <WhatsAppIcon />,
    title: "WhatsApp Oficial",
    description:
      "Atenda seus clientes utilizando a API oficial do WhatsApp Business.",
  },
  {
    icon: <UsersIcon />,
    title: "Múltiplos atendentes",
    description:
      "Sua equipe inteira pode atender clientes dentro de uma única plataforma.",
  },
  {
    icon: <BrainIcon />,
    title: "Inteligência Artificial",
    description:
      "Automatize o primeiro atendimento, qualificação e respostas frequentes.",
  },
  {
    icon: <BotIcon />,
    title: "Bot integrado",
    description:
      "Automatize etapas repetitivas e mantenha o atendimento funcionando.",
  },
  {
    icon: <TagIcon />,
    title: "Etiquetas",
    description:
      "Classifique contatos e organize as conversas de acordo com sua operação.",
  },
  {
    icon: <PipelineIcon />,
    title: "Pipeline",
    description:
      "Visualize em qual etapa cada oportunidade está e acompanhe sua evolução.",
  },
  {
    icon: <ChartIcon />,
    title: "Dashboard",
    description:
      "Acompanhe os principais indicadores da operação em um único lugar.",
  },
  {
    icon: <ZapIcon />,
    title: "Distribuição automática",
    description:
      "Direcione novos atendimentos automaticamente para sua equipe.",
  },
];

const faqs = [
  {
    question: "O ATENZ utiliza o WhatsApp oficial?",
    answer:
      "Sim. O ATENZ foi desenvolvido para trabalhar com a API oficial do WhatsApp Business da Meta.",
  },
  {
    question: "Preciso deixar um celular ligado?",
    answer:
      "Não. Com a API oficial, sua operação não depende de manter um aparelho celular conectado permanentemente.",
  },
  {
    question: "Várias pessoas podem atender o mesmo WhatsApp?",
    answer:
      "Sim. O número de atendentes disponíveis depende do plano contratado.",
  },
  {
    question: "O ATENZ possui inteligência artificial?",
    answer:
      "Sim. A plataforma possui recursos de inteligência artificial para auxiliar na qualificação, respostas e automação do atendimento.",
  },
  {
    question: "O que acontece se eu atingir o limite de uso da IA?",
    answer:
      "Sua operação continua funcionando normalmente. Você poderá utilizar as automações disponíveis no plano ou contratar capacidade adicional de IA.",
  },
  {
    question: "Posso conectar mais de um número de WhatsApp?",
    answer:
      "Sim. Os planos superiores permitem trabalhar com múltiplas linhas de WhatsApp dentro da mesma operação.",
  },
  {
    question: "Vocês ajudam na configuração?",
    answer:
      "Sim. Nossa proposta é facilitar a implantação para que sua empresa não precise lidar com toda a complexidade técnica da integração.",
  },
];

export default function LandingPage() {
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  const scrollTo = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth" });
  };

  return (
    <div className="atenz-site">

      {/* NAVBAR */}
      <header className="navbar">
        <div className="container navbar-content">
          <button className="brand" onClick={() => scrollTo("inicio")} aria-label="ATENZ">
            <img src={atenzLogo} alt="ATENZ" className="brand-symbol-image" />
            <span className="brand-name">ATEN<span>Z</span></span>
          </button>
          <nav className="nav-links">
            <button onClick={() => scrollTo("recursos")}>Recursos</button>
            <button onClick={() => scrollTo("como-funciona")}>Como funciona</button>
            <button onClick={() => scrollTo("planos")}>Planos</button>
            <button onClick={() => scrollTo("faq")}>Dúvidas</button>
          </nav>
          <div className="nav-actions">
            <a href="/login" className="login-link">Entrar</a>
            <button className="btn btn-small btn-primary" onClick={() => scrollTo("contato")}>
              Quero conhecer
            </button>
          </div>
        </div>
      </header>

      <main>

        {/* HERO */}
        <section id="inicio" className="hero">
          <div className="hero-grid" />
          <div className="hero-glow hero-glow-one" />
          <div className="hero-glow hero-glow-two" />
          <div className="container hero-content">
            <div className="hero-copy">
              <div className="eyebrow">
                <span className="eyebrow-dot" />
                API OFICIAL <span>•</span> IA <span>•</span> AUTOMAÇÃO
              </div>
              <h1>
                Seu WhatsApp<br />trabalhando para<br /><span>o seu negócio.</span>
              </h1>
              <p className="hero-description">
                Centralize seus atendimentos, organize sua equipe e automatize
                conversas com inteligência artificial em uma única plataforma.
              </p>
              <div className="hero-buttons">
                <button className="btn btn-primary btn-large" onClick={() => scrollTo("contato")}>
                  Quero conhecer o ATENZ <ArrowIcon />
                </button>
                <button className="btn btn-secondary btn-large" onClick={() => scrollTo("como-funciona")}>
                  Ver como funciona
                </button>
              </div>
              <div className="hero-trust">
                <div><CheckIcon /> WhatsApp Oficial</div>
                <div><CheckIcon /> Inteligência Artificial</div>
                <div><CheckIcon /> Múltiplos atendentes</div>
              </div>
            </div>
            <div className="hero-visual">
              <img src={atenzLogo} alt="" aria-hidden="true" className="giant-a-logo" />
              <div className="mockup-window">
                <div className="mockup-top">
                  <div className="mockup-dots"><span /><span /><span /></div>
                  <strong>Central de Atendimento</strong>
                  <div className="online-badge"><span />Online</div>
                </div>
                <div className="mockup-content">
                  <aside className="mockup-sidebar">
                    <img src={atenzLogo} alt="" className="fake-logo-img" />
                    <span className="mock-nav active" />
                    <span className="mock-nav" />
                    <span className="mock-nav" />
                    <span className="mock-nav" />
                  </aside>
                  <div className="mockup-conversations">
                    <div className="mock-search" />
                    {[1, 2, 3, 4].map((item) => (
                      <div className={`mock-conversation ${item === 1 ? "selected" : ""}`} key={item}>
                        <div className="mock-avatar" />
                        <div>
                          <strong>{item === 1 ? "Novo cliente" : `Cliente ${item}`}</strong>
                          <span>Olá, gostaria de saber...</span>
                        </div>
                      </div>
                    ))}
                  </div>
                  <div className="mockup-chat">
                    <div className="chat-header">
                      <div className="mock-avatar" />
                      <div>
                        <strong>Novo cliente</strong>
                        <span>WhatsApp</span>
                      </div>
                    </div>
                    <div className="chat-body">
                      <div className="message incoming">Olá! Gostaria de saber como funciona.</div>
                      <div className="message outgoing">Olá! 👋 Eu posso te ajudar. Você procura atendimento para quantas pessoas?</div>
                      <div className="message incoming">Somos uma equipe com 4 atendentes.</div>
                    </div>
                    <div className="chat-input">Digite sua mensagem...</div>
                  </div>
                </div>
                <div className="floating-card floating-ai">
                  <div className="floating-icon"><BrainIcon size={21} /></div>
                  <div>
                    <span>IA ATIVA</span>
                    <strong>Atendimento inteligente</strong>
                  </div>
                </div>
                <div className="floating-card floating-result">
                  <span>CONVERSAS</span>
                  <strong>+37%</strong>
                  <small>atendimentos organizados</small>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* SOCIAL PROOF */}
        <section className="trust-strip">
          <div className="container trust-content">
            <span>Uma plataforma para empresas que atendem pelo WhatsApp</span>
            <div className="market-list">
              <strong>CLÍNICAS</strong><i />
              <strong>OFICINAS</strong><i />
              <strong>ASSISTÊNCIAS</strong><i />
              <strong>COMÉRCIO</strong><i />
              <strong>SERVIÇOS</strong>
            </div>
          </div>
        </section>

        {/* PROBLEMA */}
        <section className="section problem-section">
          <div className="container">
            <div className="section-heading centered">
              <span className="section-label">O PROBLEMA</span>
              <h2>Quantos clientes sua empresa perde<br />porque ninguém respondeu?</h2>
              <p>
                Quando o WhatsApp cresce sem organização, mais mensagens podem
                significar mais problemas — e não necessariamente mais vendas.
              </p>
            </div>
            <div className="comparison-grid">
              <div className="comparison-card before">
                <div className="comparison-top">
                  <span className="comparison-status danger" /><strong>ANTES</strong>
                </div>
                <h3>WhatsApp sem gestão</h3>
                <ul>
                  <li><span>×</span> Conversas espalhadas</li>
                  <li><span>×</span> Clientes aguardando respostas</li>
                  <li><span>×</span> Atendimento duplicado</li>
                  <li><span>×</span> Falta de histórico</li>
                  <li><span>×</span> Gestor sem visão da operação</li>
                  <li><span>×</span> Oportunidades esquecidas</li>
                </ul>
              </div>
              <div className="comparison-arrow"><ArrowIcon /></div>
              <div className="comparison-card after">
                <div className="comparison-top">
                  <span className="comparison-status success" /><strong>COM ATENZ</strong>
                </div>
                <h3>Uma operação organizada</h3>
                <ul>
                  <li><CheckIcon /> Atendimento centralizado</li>
                  <li><CheckIcon /> Conversas distribuídas</li>
                  <li><CheckIcon /> Histórico organizado</li>
                  <li><CheckIcon /> IA auxiliando o atendimento</li>
                  <li><CheckIcon /> Dashboard da operação</li>
                  <li><CheckIcon /> Mais controle sobre oportunidades</li>
                </ul>
              </div>
            </div>
          </div>
        </section>

        {/* RECURSOS */}
        <section id="recursos" className="section features-section">
          <div className="features-glow" />
          <div className="container">
            <div className="section-heading">
              <span className="section-label">UMA ÚNICA PLATAFORMA</span>
              <h2>Tudo que sua equipe precisa<br />para atender melhor.</h2>
              <p>Atendimento, gestão, automação e inteligência artificial trabalhando juntos.</p>
            </div>
            <div className="features-grid">
              {features.map((feature) => (
                <article className="feature-card" key={feature.title}>
                  <div className="feature-icon">{feature.icon}</div>
                  <h3>{feature.title}</h3>
                  <p>{feature.description}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* PRODUTO */}
        <section className="section product-section">
          <div className="container product-grid">
            <div className="product-copy">
              <span className="section-label">CENTRAL DE ATENDIMENTO</span>
              <h2>Toda a sua operação<br />em uma única tela.</h2>
              <p>
                Atenda clientes, acompanhe conversas e gerencie sua equipe sem
                depender de vários celulares ou aplicativos separados.
              </p>
              <div className="product-benefits">
                <div>
                  <CheckIcon />
                  <span><strong>Distribuição inteligente</strong> Cada conversa com o atendente certo.</span>
                </div>
                <div>
                  <CheckIcon />
                  <span><strong>Histórico centralizado</strong> Sua equipe sabe exatamente o que já foi conversado.</span>
                </div>
                <div>
                  <CheckIcon />
                  <span><strong>Gestão da operação</strong> Acompanhe sua equipe e seus atendimentos.</span>
                </div>
                <div>
                  <CheckIcon />
                  <span><strong>IA integrada</strong> Automatize sem perder o controle da conversa.</span>
                </div>
              </div>
              <button className="btn btn-primary" onClick={() => scrollTo("contato")}>
                Quero ver uma demonstração <ArrowIcon />
              </button>
            </div>
            <div className="dashboard-card">
              <div className="dashboard-header">
                <div>
                  <span>Dashboard</span>
                  <h3>Visão da operação</h3>
                </div>
                <span className="live-indicator"><i />Em tempo real</span>
              </div>
              <div className="dashboard-stats">
                <div>
                  <span>Atendimentos</span>
                  <strong>128</strong>
                  <small>↑ 18% este mês</small>
                </div>
                <div>
                  <span>Em andamento</span>
                  <strong>23</strong>
                  <small>agora</small>
                </div>
                <div>
                  <span>Tempo médio</span>
                  <strong>3m</strong>
                  <small>↓ 24%</small>
                </div>
              </div>
              <div className="dashboard-chart">
                <div className="chart-title"><span>Atendimentos nos últimos dias</span></div>
                <div className="bars">
                  <span style={{ height: "28%" }} />
                  <span style={{ height: "44%" }} />
                  <span style={{ height: "39%" }} />
                  <span style={{ height: "63%" }} />
                  <span style={{ height: "54%" }} />
                  <span style={{ height: "78%" }} />
                  <span style={{ height: "90%" }} />
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* IA */}
        <section className="section ai-section">
          <div className="ai-background-letter">A</div>
          <div className="container ai-grid">
            <div className="ai-orbit">
              <div className="orbit-circle orbit-one" />
              <div className="orbit-circle orbit-two" />
              <div className="ai-center">
                <BrainIcon size={50} />
                <strong>ATENZ IA</strong>
                <span>ONLINE</span>
              </div>
              <div className="orbit-item orbit-item-one"><WhatsAppIcon /></div>
              <div className="orbit-item orbit-item-two"><UsersIcon /></div>
              <div className="orbit-item orbit-item-three"><ZapIcon /></div>
            </div>
            <div className="ai-copy">
              <span className="section-label">INTELIGÊNCIA ARTIFICIAL</span>
              <h2>Sua empresa continua<br />atendendo mesmo quando<br />sua equipe está ocupada.</h2>
              <p>
                A IA do ATENZ pode realizar o primeiro contato, entender a
                necessidade do cliente, responder perguntas e conduzir etapas
                iniciais do atendimento.
              </p>
              <div className="ai-list">
                <span><CheckIcon /> Qualificação inicial</span>
                <span><CheckIcon /> Respostas automáticas</span>
                <span><CheckIcon /> Atendimento mais rápido</span>
                <span><CheckIcon /> Transferência para atendente</span>
              </div>
              <div className="ai-quote">
                <strong>
                  IA quando automatizar faz sentido.<br />
                  <span>Pessoas quando elas fazem diferença.</span>
                </strong>
              </div>
            </div>
          </div>
        </section>

        {/* COMO FUNCIONA */}
        <section id="como-funciona" className="section steps-section">
          <div className="container">
            <div className="section-heading centered">
              <span className="section-label">SIMPLES DE USAR</span>
              <h2>Como funciona?</h2>
              <p>Seu cliente continua usando o WhatsApp normalmente. O ATENZ organiza tudo por trás.</p>
            </div>
            <div className="steps">
              <div className="step">
                <span className="step-number">01</span>
                <div className="step-icon"><WhatsAppIcon /></div>
                <h3>Cliente chama</h3>
                <p>O cliente envia uma mensagem normalmente pelo WhatsApp.</p>
              </div>
              <div className="step-line" />
              <div className="step">
                <span className="step-number">02</span>
                <div className="step-icon"><BrainIcon /></div>
                <h3>IA atende</h3>
                <p>O ATENZ identifica a necessidade e pode iniciar o atendimento.</p>
              </div>
              <div className="step-line" />
              <div className="step">
                <span className="step-number">03</span>
                <div className="step-icon"><UsersIcon /></div>
                <h3>Equipe assume</h3>
                <p>Quando necessário, a conversa é direcionada para um atendente.</p>
              </div>
              <div className="step-line" />
              <div className="step">
                <span className="step-number">04</span>
                <div className="step-icon"><ChartIcon /></div>
                <h3>Tudo fica organizado</h3>
                <p>Histórico, responsáveis e informações permanecem centralizados.</p>
              </div>
            </div>
          </div>
        </section>

        {/* SEGMENTOS */}
        <section className="section industries-section">
          <div className="container">
            <div className="section-heading centered">
              <span className="section-label">PARA QUEM É O ATENZ?</span>
              <h2>Se o WhatsApp faz parte do negócio,<br />o ATENZ faz parte da operação.</h2>
            </div>
            <div className="industries-grid">
              {[
                ["🏥", "Clínicas"],
                ["🔧", "Oficinas"],
                ["📱", "Assistências técnicas"],
                ["🏪", "Comércio"],
                ["🏢", "Prestadores de serviço"],
                ["🏠", "Imobiliárias"],
                ["📊", "Escritórios"],
                ["💼", "Pequenas empresas"],
              ].map(([icon, title]) => (
                <div className="industry-card" key={title}>
                  <span>{icon}</span>
                  <strong>{title}</strong>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* PLANOS */}
        <section id="planos" className="section pricing-section">
          <div className="pricing-glow" />
          <div className="container">
            <div className="section-heading centered">
              <span className="section-label">PLANOS</span>
              <h2>Comece no tamanho da sua operação.</h2>
              <p>Escolha o plano ideal para sua equipe e evolua conforme sua empresa crescer.</p>
            </div>
            <div className="pricing-grid">

              {/* STAR */}
              <article className="pricing-card">
                <div className="plan-header">
                  <span className="plan-name">STAR</span>
                  <p>O essencial para começar com profissionalismo.</p>
                </div>
                <div className="price">
                  <span>R$</span><strong>149</strong><small>/mês</small>
                </div>
                <div className="plan-main-info">
                  <div><WhatsAppIcon /><span><strong>1</strong> WhatsApp Oficial</span></div>
                  <div><UsersIcon /><span>Até 2 atendentes</span></div>
                </div>
                <ul className="plan-features">
                  <li><CheckIcon /> API oficial da Meta</li>
                  <li><CheckIcon /> Dashboard</li>
                  <li><CheckIcon /> IA para qualificação inicial</li>
                  <li><CheckIcon /> Histórico de conversas</li>
                  <li><CheckIcon /> Atendimento compartilhado</li>
                </ul>
                <button className="btn btn-plan" onClick={() => scrollTo("contato")}>Começar com STAR</button>
              </article>

              {/* FLOW */}
              <article className="pricing-card featured">
                <span className="popular-badge">MAIS ESCOLHIDO</span>
                <div className="plan-header">
                  <span className="plan-name">FLOW</span>
                  <p>Para ganhar agilidade no atendimento.</p>
                </div>
                <div className="price">
                  <span>R$</span><strong>299</strong><small>/mês</small>
                </div>
                <div className="plan-main-info">
                  <div><WhatsAppIcon /><span><strong>2</strong> linhas conectadas</span></div>
                  <div><UsersIcon /><span>Até 4 atendentes</span></div>
                </div>
                <ul className="plan-features">
                  <li><CheckIcon /> Tudo do STAR</li>
                  <li><CheckIcon /> IA para atendimento inteligente</li>
                  <li><CheckIcon /> Bot integrado</li>
                  <li><CheckIcon /> Etiquetas</li>
                  <li><CheckIcon /> Distribuição automática</li>
                  <li><CheckIcon /> Pipeline</li>
                </ul>
                <button className="btn btn-primary btn-plan" onClick={() => scrollTo("contato")}>Quero o FLOW</button>
              </article>

              {/* SCALA */}
              <article className="pricing-card">
                <div className="plan-header">
                  <span className="plan-name">SCALA</span>
                  <p>Leve sua operação para o próximo nível.</p>
                </div>
                <div className="price">
                  <span>R$</span><strong>497</strong><small>/mês</small>
                </div>
                <div className="plan-main-info">
                  <div><WhatsAppIcon /><span><strong>3</strong> linhas conectadas</span></div>
                  <div><UsersIcon /><span>Até 8 atendentes</span></div>
                </div>
                <ul className="plan-features">
                  <li><CheckIcon /> Tudo do FLOW</li>
                  <li><CheckIcon /> IA para atendimento avançado</li>
                  <li><CheckIcon /> Etiquetas de qualificação</li>
                  <li><CheckIcon /> Relatório de performance</li>
                  <li><CheckIcon /> Respostas rápidas</li>
                  <li><CheckIcon /> Pipeline completo</li>
                </ul>
                <button className="btn btn-plan" onClick={() => scrollTo("contato")}>Quero o SCALA</button>
              </article>

            </div>
            <div className="pricing-note">
              <div className="pricing-note-icon">i</div>
              <p>
                Todos os planos possuem uma franquia de uso de inteligência
                artificial. Caso sua operação precise de maior capacidade,
                recursos adicionais poderão ser contratados.
              </p>
            </div>
          </div>
        </section>

        {/* IMPLANTAÇÃO */}
        <section className="section implementation-section">
          <div className="container implementation-grid">
            <div className="implementation-copy">
              <span className="section-label">IMPLANTAÇÃO</span>
              <h2>Você não contrata<br />apenas um sistema.</h2>
              <p>Ajudamos sua empresa a preparar a operação para começar a usar o ATENZ.</p>
            </div>
            <div className="implementation-list">
              <div><span>01</span><article><h3>Configuração</h3><p>Preparamos a estrutura inicial da sua empresa dentro da plataforma.</p></article></div>
              <div><span>02</span><article><h3>Integração</h3><p>Auxiliamos na integração do seu WhatsApp com a API oficial.</p></article></div>
              <div><span>03</span><article><h3>Equipe</h3><p>Organizamos os acessos e atendentes conforme sua operação.</p></article></div>
              <div><span>04</span><article><h3>IA e automações</h3><p>Configuramos os recursos disponíveis conforme o plano contratado.</p></article></div>
            </div>
          </div>
        </section>

        {/* FAQ */}
        <section id="faq" className="section faq-section">
          <div className="container faq-layout">
            <div className="faq-copy">
              <span className="section-label">DÚVIDAS FREQUENTES</span>
              <h2>Tudo que você precisa<br />saber antes de começar.</h2>
              <p>Ainda ficou alguma dúvida? Entre em contato com nossa equipe.</p>
              <button className="btn btn-secondary" onClick={() => scrollTo("contato")}>
                Falar com a equipe
              </button>
            </div>
            <div className="faq-list">
              {faqs.map((faq, index) => (
                <article className={`faq-item ${openFaq === index ? "open" : ""}`} key={faq.question}>
                  <button onClick={() => setOpenFaq(openFaq === index ? null : index)}>
                    <span>{faq.question}</span>
                    <strong>{openFaq === index ? "−" : "+"}</strong>
                  </button>
                  <div className="faq-answer">
                    <p>{faq.answer}</p>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>

        {/* CTA */}
        <section id="contato" className="final-cta">
          <div className="cta-glow cta-glow-one" />
          <div className="cta-glow cta-glow-two" />
          <div className="container final-cta-content">
            <div className="cta-logo"><img src={atenzLogo} alt="ATENZ" className="cta-logo-img" /></div>
            <span className="section-label">COMECE AGORA</span>
            <h2>Seu próximo cliente provavelmente<br />vai falar com você pelo WhatsApp.</h2>
            <p>
              Não deixe essa conversa se perder. Organize sua equipe, automatize
              tarefas e transforme o WhatsApp em um canal profissional de atendimento.
            </p>
            <a
              className="btn btn-primary btn-large"
              href="https://wa.me/5500000000000"
              target="_blank"
              rel="noreferrer"
            >
              <WhatsAppIcon /> Quero conhecer o ATENZ
            </a>
            <strong className="cta-slogan">
              MAIS <span>AGILIDADE.</span> MAIS <span>RESULTADO.</span>
            </strong>
          </div>
        </section>

      </main>

      {/* FOOTER */}
      <footer className="footer">
        <div className="container footer-main">
          <div className="footer-brand">
            <div className="brand">
              <img src={atenzLogo} alt="ATENZ" className="brand-symbol-image" />
              <span className="brand-name">ATEN<span>Z</span></span>
            </div>
            <p>Atendimento inteligente para empresas que querem crescer com organização, automação e tecnologia.</p>
            <span className="footer-tag">API Oficial • IA • Automação</span>
          </div>
          <div className="footer-column">
            <strong>ATENZ</strong>
            <button onClick={() => scrollTo("recursos")}>Recursos</button>
            <button onClick={() => scrollTo("como-funciona")}>Como funciona</button>
            <button onClick={() => scrollTo("planos")}>Planos</button>
          </div>
          <div className="footer-column">
            <strong>SUPORTE</strong>
            <button onClick={() => scrollTo("faq")}>Dúvidas frequentes</button>
            <a href="/login">Área do cliente</a>
          </div>
          <div className="footer-column">
            <strong>CONTATO</strong>
            <a href="https://wa.me/5500000000000" target="_blank" rel="noreferrer">WhatsApp</a>
            <a href="mailto:contato@atenz.com.br">contato@atenz.com.br</a>
          </div>
        </div>
        <div className="container footer-bottom">
          <span>© 2026 ATENZ. Todos os direitos reservados.</span>
          <div>
            <a href="/privacidade">Política de Privacidade</a>
            <a href="/termos">Termos de Uso</a>
          </div>
        </div>
      </footer>

    </div>
  );
}
