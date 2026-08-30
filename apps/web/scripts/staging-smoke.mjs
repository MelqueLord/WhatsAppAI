import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'

const baseUrl = required('STAGING_BASE_URL').replace(/\/$/, '')
const email = required('STAGING_EMAIL')
const password = required('STAGING_PASSWORD')
const qrLineNumber = Number(required('STAGING_QR_LINE_NUMBER'))

if (!Number.isInteger(qrLineNumber) || qrLineNumber < 1) {
  fail('STAGING_QR_LINE_NUMBER must be a positive integer')
}

const checks = []

await check('API liveness', async () => expectStatus(await fetch(`${baseUrl}/health/live`), 200))
await check('API readiness', async () => expectStatus(await fetch(`${baseUrl}/health/ready`), 200))

const login = await request('/api/auth/login', {
  method: 'POST',
  body: { email, password },
})
if (!login.token) fail('Staging login did not return a bearer token')
checks.push('authentication')

const headers = { Authorization: `Bearer ${login.token}` }
const me = await request('/api/auth/me', { headers })
if (!me.tenantId || !me.role) fail('Authenticated staging user has no tenant context')
checks.push('tenant isolation context')

const providers = await request('/api/integrations/ai/providers', { headers })
if (!Array.isArray(providers) || providers.length === 0) fail('AI provider catalog is empty')
checks.push('AI provider catalog')

const aiConfig = await request('/api/integrations/ai', { headers })
if (!aiConfig.configured) fail('AI provider is not configured in staging')
const aiConnection = await request('/api/integrations/ai/test-connection', {
  method: 'POST',
  headers,
})
if (aiConnection.success !== true) fail('AI provider connection test failed')
checks.push('AI provider connection')

const whatsappConfig = await request('/api/integrations/whatsapp', { headers })
if (!Array.isArray(whatsappConfig.lines) || whatsappConfig.lines.length === 0) {
  fail('WhatsApp Cloud/QR line is not configured in staging')
}
const officialLine = whatsappConfig.lines.find((line) =>
  String(line.connectionType).toLowerCase() === 'officialapi')
const qrLine = whatsappConfig.lines.find((line) =>
  String(line.connectionType).toLowerCase() === 'qrcode' && line.lineNumber === qrLineNumber)
if (!officialLine) fail('A WhatsApp Cloud API line is required in staging')
if (!qrLine) fail(`QR Code line ${qrLineNumber} is not configured in staging`)
const cloudConnection = await request('/api/integrations/whatsapp/test-connection', {
  method: 'POST',
  headers,
})
if (cloudConnection.success !== true) fail('WhatsApp Cloud API connection test failed')
checks.push('WhatsApp Cloud API connection')

const qrStatus = await request(`/api/integrations/whatsapp/session/status/${qrLineNumber}`, { headers })
if (typeof qrStatus.status !== 'string' || typeof qrStatus.isConnected !== 'boolean') {
  fail('QR Code session status response is invalid')
}
checks.push('WhatsApp QR session')

const hub = new HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/inbox`, {
    accessTokenFactory: () => login.token,
    transport: HttpTransportType.WebSockets,
  })
  .configureLogging(LogLevel.Error)
  .build()

try {
  await hub.start()
  if (hub.state !== 'Connected') fail('SignalR did not reach Connected state')
  checks.push('SignalR tenant connection')
} finally {
  await hub.stop()
}

for (const item of checks) console.log(`PASS ${item}`)

async function check(name, action) {
  await action()
  checks.push(name)
}

async function request(path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: { Accept: 'application/json', ...(options.headers ?? {}) },
    ...(options.body === undefined ? {} : { body: JSON.stringify(options.body), headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(options.headers ?? {}) } }),
  })
  if (!response.ok) {
    await response.arrayBuffer()
    fail(`${path} returned HTTP ${response.status}`)
  }
  return response.status === 204 ? {} : response.json()
}

async function expectStatus(response, status) {
  if (response.status !== status) fail(`Expected HTTP ${status}, received ${response.status}`)
}

function required(name) {
  const value = process.env[name]
  if (!value) fail(`${name} is required`)
  return value
}

function fail(message) {
  console.error(`FAIL ${message}`)
  process.exit(1)
}
