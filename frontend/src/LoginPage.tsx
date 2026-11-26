import { useState, type FormEvent } from 'react'
import { saveToken } from './auth'

// Goes through the Vite proxy to Registration's server (see vite.config.ts).
const LOGIN_URL = '/registration-api/api/Login/authenticate'

interface LoginPageProps {
  onLoggedIn: () => void
}

// Registration's reply might be { token: "..." } or just the token text.
function extractToken(text: string): string | null {
  const trimmed = text.trim()
  try {
    const data: unknown = JSON.parse(trimmed)
    if (typeof data === 'string') return data
    if (data && typeof data === 'object') {
      const obj = data as Record<string, unknown>
      for (const key of ['token', 'accessToken', 'access_token', 'jwt', 'jwtToken']) {
        const value = obj[key]
        if (typeof value === 'string' && value) return value
      }
    }
    return null
  } catch {
    return trimmed.split('.').length === 3 ? trimmed : null
  }
}

export default function LoginPage({ onLoggedIn }: LoginPageProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError('')
    setLoading(true)

    try {
      const res = await fetch(LOGIN_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password, rememberMe: true }),
      })
      const text = await res.text()

      if (!res.ok) {
        setError(
          res.status === 400 || res.status === 401
            ? 'Wrong username or password.'
            : `Login failed (HTTP ${res.status}).`
        )
        return
      }

      const token = extractToken(text)
      if (!token) {
        setError('Login worked but no token came back.')
        return
      }

      saveToken(token)
      onLoggedIn()
    } catch {
      setError('Login service is unavailable.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main style={{ maxWidth: 360, margin: '4rem auto' }}>
            <h1 style={{ fontSize: '2rem', lineHeight: 1.3 }}>Budget &amp; Finance</h1>
      <h2>Log in</h2>
      <form onSubmit={handleSubmit}>
        <p>
          <label>
            Username
            <br />
            <input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              required
              style={{ width: '100%' }}
            />
          </label>
        </p>
        <p>
          <label>
            Password
            <br />
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
              style={{ width: '100%' }}
            />
          </label>
        </p>
        {error && (
          <p role="alert" style={{ color: 'red' }}>
            {error}
          </p>
        )}
        <button type="submit" disabled={loading}>
          {loading ? 'Logging in...' : 'Log in'}
        </button>
      </form>
    </main>
  )
}