export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(
    message: string,
    status: number,
    code: string,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

type ErrorPayload = {
  code?: unknown
  error?: unknown
  message?: unknown
  detail?: unknown
  title?: unknown
}

const genericServerMessages = new Set([
  'Bad Request',
  'Unauthorized',
  'Forbidden',
  'Not Found',
  'Conflict',
  'Internal Server Error',
])

function messageForStatus(status: number): string {
  switch (status) {
    case 400:
      return 'Confira os dados informados e tente novamente.'
    case 401:
      return 'Sua sessão expirou. Entre novamente para continuar.'
    case 403:
      return 'Você não tem permissão para realizar esta ação.'
    case 404:
      return 'Não encontramos a informação solicitada. Atualize a página e tente novamente.'
    case 409:
      return 'Esta informação foi alterada por outra pessoa. Atualize a página e tente novamente.'
    case 413:
      return 'O arquivo enviado é maior do que o permitido.'
    case 429:
      return 'Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente novamente.'
    default:
      return 'Não foi possível concluir agora. Tente novamente em alguns instantes.'
  }
}

function isSafeServerMessage(value: unknown): value is string {
  if (typeof value !== 'string') return false

  const message = value.trim()
  if (!message || message.length > 280 || genericServerMessages.has(message)) return false

  return !/(exception|stack trace| at [\w.]+\(|select |insert |update |delete |postgres|npgsql)/i.test(message)
}

function isUserFacingMessage(value: string): boolean {
  return isSafeServerMessage(value) && /^(não|você|esta|confira|a |o |selecione|reduza|e-mail|as senhas|erro ao|falha ao)/i.test(value.trim())
}

export async function createApiError(response: Response): Promise<ApiError> {
  const fallback = messageForStatus(response.status)
  let payload: ErrorPayload | null = null

  if (typeof response.text === 'function') {
    const body = await response.text().catch(() => '')
    try {
      payload = JSON.parse(body) as ErrorPayload
    } catch {
      // A resposta pode não ter corpo JSON. Nesse caso, usamos a mensagem segura do status.
    }
  } else if (typeof response.json === 'function') {
    payload = await response.json().catch(() => null) as ErrorPayload | null
  }

  const serverMessage = payload && [
    payload.error,
    payload.message,
    payload.detail,
    payload.title,
  ].find(isSafeServerMessage)

  const code = typeof payload?.code === 'string'
    ? payload.code
    : `HTTP_${response.status}`

  return new ApiError(serverMessage ?? fallback, response.status, code)
}

export function friendlyErrorMessage(error: unknown, fallback?: string): string {
  if (error instanceof ApiError) return error.message

  if (error instanceof TypeError) {
    return 'Não foi possível conectar ao sistema. Verifique sua internet e tente novamente.'
  }

  if (error instanceof Error) {
    if (error.message === 'INVALID_CREDENTIALS') return 'E-mail ou senha incorretos.'
    if (error.message === 'UNAUTHORIZED') return 'Sua sessão expirou. Entre novamente para continuar.'
    if (error.message === 'NETWORK_ERROR') return 'Não foi possível conectar ao sistema. Verifique sua internet e tente novamente.'

    if (isUserFacingMessage(error.message)) return error.message
  }

  return fallback ?? 'Não foi possível concluir agora. Tente novamente em alguns instantes.'
}
