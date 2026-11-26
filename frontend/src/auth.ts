const TOKEN_KEY = 'finance_token'

// ---- token storage (wrapped in try/catch in case the browser blocks storage) ----
export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function saveToken(token: string) {
  try {
    localStorage.setItem(TOKEN_KEY, token)
  } catch {
    // ignore
  }
}

export function clearToken() {
  try {
    localStorage.removeItem(TOKEN_KEY)
  } catch {
    // ignore
  }
}

// ---- reading the JWT payload (the middle part of the token) ----
function decodePayload(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1]
    if (!part) return null
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join('')
    )
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return null
  }
}

function payloadOf(token: string | null): Record<string, unknown> | null {
  return token ? decodePayload(token) : null
}

// Logged in = there is a token and it has not expired.
export function isLoggedIn(): boolean {
  const token = getToken()
  if (!token) return false
  const payload = payloadOf(token)
  if (payload && typeof payload.exp === 'number') {
    return payload.exp * 1000 > Date.now()
  }
  return true
}

// The role can come under a few different claim names, so we check them all.
const ROLE_CLAIMS = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
]

export function getRoles(): string[] {
  const payload = payloadOf(getToken())
  if (!payload) return []
  for (const claim of ROLE_CLAIMS) {
    const value = payload[claim]
    if (typeof value === 'string') return [value]
    if (Array.isArray(value)) {
      return value.filter((x): x is string => typeof x === 'string')
    }
  }
  return []
}

export function isManager(): boolean {
  return getRoles().some((r) => r.toLowerCase() === 'manager')
}

export function getUserName(): string {
  const payload = payloadOf(getToken())
  if (!payload) return ''
  const keys = [
    'unique_name',
    'name',
    'preferred_username',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
    'sub',
  ]
  for (const key of keys) {
    const value = payload[key]
    if (typeof value === 'string' && value) return value
  }
  return ''
}

// ---- one helper that adds the Bearer token to every /api/ call ----
let installed = false

export function installAuthFetch() {
  if (installed) return
  installed = true

  const originalFetch = window.fetch.bind(window)

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url

    // Only our own API calls get the token. The login call does not.
    if (!url.startsWith('/api/')) {
      return originalFetch(input, init)
    }

    const headers = new Headers(
      init?.headers ?? (input instanceof Request ? input.headers : undefined)
    )
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    const response = await originalFetch(input, { ...init, headers })

    // Token missing, wrong or expired: go back to the login page.
    if (response.status === 401) {
      clearToken()
      window.location.reload()
    }
    return response
  }
}