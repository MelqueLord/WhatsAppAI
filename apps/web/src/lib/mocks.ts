const now = Date.now()
const h = (hours: number) => new Date(now - hours * 3600000).toISOString()
const m = (minutes: number) => new Date(now - minutes * 60000).toISOString()

export const mockUser = {
  id: 'u-1',
  email: 'admin@whatsapp.ai',
  displayName: 'Admin Demo',
  tenantId: 't-1',
  role: 'TenantOwner',
  isPlatformAdmin: false,
}

export const mockConversations = [
  {
    id: 'c-1',
    contactName: 'Maria Silva',
    contactPhone: '+55 11 99999-1234',
    mode: 'Automatic',
    status: 'Open',
    lastMessage: 'Olá! Gostaria de saber sobre os preços.',
    lastMessageAt: m(5),
    isWindowOpen: true,
    version: 3,
  },
  {
    id: 'c-2',
    contactName: 'João Santos',
    contactPhone: '+55 11 98888-5678',
    mode: 'Human',
    status: 'Open',
    lastMessage: 'Obrigado pelo atendimento!',
    lastMessageAt: m(30),
    isWindowOpen: true,
    version: 5,
  },
  {
    id: 'c-3',
    contactName: 'Ana Oliveira',
    contactPhone: '+55 21 97777-9012',
    mode: 'Automatic',
    status: 'Open',
    lastMessage: 'Preciso de ajuda com meu pedido #12345',
    lastMessageAt: h(2),
    isWindowOpen: true,
    version: 2,
  },
  {
    id: 'c-4',
    contactName: 'Pedro Costa',
    contactPhone: '+55 31 96666-3456',
    mode: 'Paused',
    status: 'Open',
    lastMessage: 'Quando vocês abrem amanhã?',
    lastMessageAt: h(5),
    isWindowOpen: false,
    version: 1,
  },
  {
    id: 'c-5',
    contactName: 'Carla Mendes',
    contactPhone: '+55 41 95555-7890',
    mode: 'Automatic',
    status: 'Open',
    lastMessage: 'Vocês fazem entrega na minha região?',
    lastMessageAt: h(24),
    isWindowOpen: true,
    version: 4,
  },
  {
    id: 'c-6',
    contactName: 'Lucas Ferreira',
    contactPhone: '+55 51 94444-1234',
    mode: 'Human',
    status: 'Open',
    lastMessage: 'Preciso falar com o gerente.',
    lastMessageAt: h(48),
    isWindowOpen: false,
    version: 2,
  },
]

export const mockMessages: Record<string, any[]> = {
  'c-1': [
    { id: 'm-1-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Olá! Tudo bem?', createdAt: m(15), senderName: 'Maria Silva' },
    { id: 'm-1-2', direction: 'Outbound', status: 'Read', type: 'Text', content: 'Olá Maria! Tudo ótimo, e você? Como posso ajudar?', createdAt: m(14) },
    { id: 'm-1-3', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Gostaria de saber sobre os preços dos seus serviços.', createdAt: m(5), senderName: 'Maria Silva' },
  ],
  'c-2': [
    { id: 'm-2-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Preciso de ajuda com minha conta.', createdAt: h(1), senderName: 'João Santos' },
    { id: 'm-2-2', direction: 'Outbound', status: 'Read', type: 'Text', content: 'Claro João! Qual é o problema?', createdAt: m(55) },
    { id: 'm-2-3', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Não consigo acessar o painel.', createdAt: m(50), senderName: 'João Santos' },
    { id: 'm-2-4', direction: 'Outbound', status: 'Delivered', type: 'Text', content: 'Vou verificar isso para você. Pode me informar seu email?', createdAt: m(45) },
    { id: 'm-2-5', direction: 'Inbound', status: 'Read', type: 'Text', content: 'joao@email.com', createdAt: m(40), senderName: 'João Santos' },
    { id: 'm-2-6', direction: 'Outbound', status: 'Delivered', type: 'Text', content: 'Encontrei o problema! Sua conta estava suspensa por pagamento. Já reativei.', createdAt: m(35) },
    { id: 'm-2-7', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Obrigado pelo atendimento!', createdAt: m(30), senderName: 'João Santos' },
  ],
  'c-3': [
    { id: 'm-3-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Boa tarde!', createdAt: h(3), senderName: 'Ana Oliveira' },
    { id: 'm-3-2', direction: 'Outbound', status: 'Read', type: 'Text', content: 'Boa tarde Ana! Como posso ajudar?', createdAt: h(2.5) },
    { id: 'm-3-3', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Preciso de ajuda com meu pedido #12345', createdAt: h(2), senderName: 'Ana Oliveira' },
  ],
  'c-4': [
    { id: 'm-4-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Quando vocês abrem amanhã?', createdAt: h(5), senderName: 'Pedro Costa' },
  ],
  'c-5': [
    { id: 'm-5-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Vocês fazem entrega na minha região?', createdAt: h(25), senderName: 'Carla Mendes' },
    { id: 'm-5-2', direction: 'Outbound', status: 'Read', type: 'Text', content: 'Sim! Qual é o seu CEP?', createdAt: h(24) },
  ],
  'c-6': [
    { id: 'm-6-1', direction: 'Inbound', status: 'Read', type: 'Text', content: 'Preciso falar com o gerente.', createdAt: h(48), senderName: 'Lucas Ferreira' },
  ],
}

export const mockOperators = [
  { id: 'op-1', email: 'operador1@empresa.com', displayName: 'Carlos Souza', role: 'Operator', isActive: true, createdAt: h(720) },
  { id: 'op-2', email: 'operador2@empresa.com', displayName: 'Fernanda Lima', role: 'Operator', isActive: true, createdAt: h(480) },
  { id: 'op-3', email: 'operador3@empresa.com', displayName: 'Ricardo Alves', role: 'Operator', isActive: false, createdAt: h(360) },
]

export const mockKnowledge = [
  { id: 'k-1', title: 'Horário de atendimento', content: 'Segunda a sexta das 8h às 18h. Sábado das 8h às 12h.', priority: 10, isActive: true, version: 1 },
  { id: 'k-2', title: 'Política de devolução', content: 'Devoluções em até 30 dias com nota fiscal. Produto sem uso.', priority: 8, isActive: true, version: 2 },
  { id: 'k-3', title: 'Formas de pagamento', content: 'Aceitamos PIX, cartão de crédito/débito e boleto. Parcelamento em até 12x.', priority: 7, isActive: true, version: 1 },
  { id: 'k-4', title: 'Frete grátis', content: 'Frete grátis para compras acima de R$ 199,00 em todo o Brasil.', priority: 6, isActive: true, version: 1 },
  { id: 'k-5', title: 'Promoção desativada', content: 'Black Friday 2025 - já encerrada.', priority: 0, isActive: false, version: 3 },
]

export const mockUsage = {
  from: new Date(now - 30 * 86400000).toISOString(),
  to: new Date(now).toISOString(),
  entries: [
    { provider: 'openai', metric: 'input_tokens', totalQuantity: 245000, totalCostMinorUnits: 245, currency: 'USD', unit: 'tokens', count: 89 },
    { provider: 'openai', metric: 'output_tokens', totalQuantity: 82000, totalCostMinorUnits: 984, currency: 'USD', unit: 'tokens', count: 89 },
    { provider: 'meta', metric: 'messages_sent', totalQuantity: 342, totalCostMinorUnits: 0, currency: null, unit: 'messages', count: 342 },
    { provider: 'meta', metric: 'messages_received', totalQuantity: 518, totalCostMinorUnits: 0, currency: null, unit: 'messages', count: 518 },
  ],
  disclaimer: 'Estimativas de uso. Não é uma fatura oficial do provedor.',
}

export const mockWebhookEvents = [
  { id: 'wh-1', phoneNumberId: '123456', status: 'Processed', idempotencyKey: 'evt-001', createdAt: h(1), processedAt: h(0.9) },
  { id: 'wh-2', phoneNumberId: '123456', status: 'Processed', idempotencyKey: 'evt-002', createdAt: h(2), processedAt: h(1.9) },
  { id: 'wh-3', phoneNumberId: '123456', status: 'Failed', idempotencyKey: 'evt-003', createdAt: h(3), processedAt: null },
  { id: 'wh-4', phoneNumberId: '123456', status: 'Processed', idempotencyKey: 'evt-004', createdAt: h(5), processedAt: h(4.8) },
]

export const mockTenants = [
  { id: 't-1', name: 'Empresa Demo', status: 'Active', createdAt: h(720), version: 1 },
  { id: 't-2', name: 'Loja Virtual Ltda', status: 'Active', createdAt: h(480), version: 2 },
  { id: 't-3', name: 'Restaurante Bom Sabor', status: 'Suspended', createdAt: h(360), suspendedAt: h(48), suspensionReason: 'Pagamento pendente', version: 3 },
]

export const mockWhatsAppConfig = {
  configured: true,
  wabaId: 'waba-123',
  phoneNumberId: 'phone-456',
  isActive: true,
  version: 1,
}

export const mockAiConfig = {
  configured: true,
  provider: 'OpenAI',
  modelId: 'gpt-4o-mini',
  isActive: true,
  version: 1,
}

export const mockClientTags = [
  { id: 'tag-vip', name: 'VIP', color: '#10B981', description: 'Clientes VIP com prioridade', isActive: true },
  { id: 'tag-new', name: 'Novo', color: '#3B82F6', description: 'Clientes novos', isActive: true },
  { id: 'tag-returning', name: 'Recorrente', color: '#8B5CF6', description: 'Clientes que retornam', isActive: true },
  { id: 'tag-support', name: 'Suporte', color: '#F59E0B', description: 'Precisa de suporte técnico', isActive: true },
  { id: 'tag-b2b', name: 'B2B', color: '#EC4899', description: 'Cliente empresarial', isActive: true },
  { id: 'tag-inactive', name: 'Inativo', color: '#6B7280', description: 'Cliente inativo', isActive: false },
]

export const mockContactTags: Record<string, string[]> = {
  'c-1': ['tag-vip', 'tag-returning'],
  'c-2': ['tag-support'],
  'c-3': ['tag-new'],
  'c-5': ['tag-b2b'],
}

export const mockBotConfig = {
  configured: true,
  mode: 'SimpleAutoReply',
  welcomeMessage: 'Olá! Bem-vindo à nossa empresa. Como posso ajudar?',
  offlineMessage: 'No momento estamos fora do horário de atendimento. Deixe sua mensagem que retornaremos em breve.',
  fallbackMessage: 'Desculpe, não entendi. Pode reformular sua pergunta?',
  maxTokensPerResponse: 500,
  enabled: true,
  version: 1,
}
