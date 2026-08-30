import { appendFileSync } from 'node:fs'
import { performance } from 'node:perf_hooks'

const baseUrl = required('PERF_BASE_URL').replace(/\/$/, '')
const scenario = process.env.PERF_SCENARIO ?? 'health'
const requests = positiveInteger('PERF_REQUESTS', 200)
const concurrency = Math.min(100, positiveInteger('PERF_CONCURRENCY', 20))
const timeoutMs = positiveInteger('PERF_TIMEOUT_MS', 10_000)

const targets = {
  health: { path: '/health/live', expectedStatus: 200, p95TargetMs: 1_000 },
  inbox: { path: '/api/conversations?limit=50', expectedStatus: 200, p95TargetMs: 3_000 },
}

const target = targets[scenario]
if (!Object.hasOwn(targets, scenario)) {
  fail(`PERF_SCENARIO must be one of: ${Object.keys(targets).join(', ')}`)
}

const headers = { Accept: 'application/json' }
if (scenario === 'inbox') {
  const token = await login()
  headers.Authorization = `Bearer ${token}`
}

const durations = []
let failures = 0
let nextRequest = 0

async function runWorker() {
  while (true) {
    const requestNumber = nextRequest++
    if (requestNumber >= requests) return

    const startedAt = performance.now()
    try {
      const response = await fetch(`${baseUrl}${target.path}`, {
        headers,
        signal: AbortSignal.timeout(timeoutMs),
      })
      durations.push(performance.now() - startedAt)
      if (response.status !== target.expectedStatus) failures++
    } catch {
      durations.push(performance.now() - startedAt)
      failures++
    }
  }
}

const startedAt = Date.now()
await Promise.all(Array.from({ length: concurrency }, runWorker))
const elapsedMs = Date.now() - startedAt
const sorted = [...durations].sort((a, b) => a - b)
const p50 = percentile(sorted, 0.5)
const p95 = percentile(sorted, 0.95)
const p99 = percentile(sorted, 0.99)
const throughput = elapsedMs === 0 ? 0 : (requests / elapsedMs) * 1_000
const passed = failures === 0 && p95 < target.p95TargetMs

const report = [
  `## Performance run (${new Date().toISOString()})`,
  '',
  `- Scenario: ${scenario}`,
  `- Target: ${baseUrl}${target.path}`,
  `- Requests: ${requests}`,
  `- Concurrency: ${concurrency}`,
  `- Errors: ${failures}`,
  `- Throughput: ${throughput.toFixed(2)} req/s`,
  '',
  '| Metric | Result | Target | Status |',
  '|---|---:|---:|---|',
  `| p50 | ${p50.toFixed(2)} ms | informational | — |`,
  `| p95 | ${p95.toFixed(2)} ms | < ${target.p95TargetMs} ms | ${p95 < target.p95TargetMs ? 'PASS' : 'FAIL'} |`,
  `| p99 | ${p99.toFixed(2)} ms | informational | — |`,
  `| HTTP errors | ${failures} | 0 | ${failures === 0 ? 'PASS' : 'FAIL'} |`,
].join('\n')

console.log(report)
if (process.env.PERF_REPORT_PATH) appendFileSync(process.env.PERF_REPORT_PATH, `${report}\n`)
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${report}\n`)
if (!passed) process.exitCode = 1

async function login() {
  const email = required('STAGING_EMAIL')
  const password = required('STAGING_PASSWORD')
  const response = await fetch(`${baseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, password }),
    signal: AbortSignal.timeout(timeoutMs),
  })

  if (!response.ok) fail(`Staging login failed with HTTP ${response.status}`)
  const payload = await response.json()
  if (typeof payload.token !== 'string' || payload.token.length === 0) {
    fail('Staging login did not return a bearer token')
  }
  return payload.token
}

function percentile(values, ratio) {
  if (values.length === 0) return Number.POSITIVE_INFINITY
  const index = Math.min(values.length - 1, Math.ceil(values.length * ratio) - 1)
  return values[index]
}

function positiveInteger(name, fallback) {
  const value = process.env[name]
  if (value === undefined) return fallback
  const parsed = Number(value)
  if (!Number.isInteger(parsed) || parsed < 1) fail(`${name} must be a positive integer`)
  return parsed
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
