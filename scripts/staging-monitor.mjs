const baseUrl = required('STAGING_BASE_URL').replace(/\/$/, '')
const alertWebhook = process.env.STAGING_ALERT_WEBHOOK_URL

const checks = ['/health/live', '/health/ready']
const failures = []

for (const path of checks) {
  try {
    const response = await fetch(`${baseUrl}${path}`, { signal: AbortSignal.timeout(10_000) })
    if (!response.ok) failures.push(`${path}: HTTP ${response.status}`)
    else console.log(`PASS ${path}`)
  } catch (error) {
    failures.push(`${path}: ${error instanceof Error ? error.name : 'request failed'}`)
  }
}

if (failures.length > 0) {
  const message = `WhatsAppAI staging health check failed: ${failures.join(', ')}`
  console.error(`FAIL ${message}`)
  if (alertWebhook) {
    await fetch(alertWebhook, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: message }),
    }).catch(() => undefined)
  }
  process.exit(1)
}

function required(name) {
  const value = process.env[name]
  if (!value) {
    console.error(`FAIL ${name} is required`)
    process.exit(1)
  }
  return value
}
