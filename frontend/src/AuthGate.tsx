import { useState } from 'react'
import App from './App'
import LoginPage from './LoginPage'
import {
  clearToken,
  getRoles,
  getUserName,
  installAuthFetch,
  isLoggedIn,
} from './auth'

installAuthFetch() // runs once when the page loads

export default function AuthGate() {
  const [loggedIn, setLoggedIn] = useState(isLoggedIn())

  function logout() {
    clearToken()
    setLoggedIn(false)
  }

  if (!loggedIn) {
    return <LoginPage onLoggedIn={() => setLoggedIn(true)} />
  }

  const roles = getRoles().join(', ') || 'no role'

  return (
    <>
      <div
        style={{
          display: 'flex',
          justifyContent: 'flex-end',
          alignItems: 'center',
          gap: '0.75rem',
          padding: '0.5rem 1rem',
        }}
      >
        <span>
          Logged in as <strong>{getUserName() || 'user'}</strong> ({roles})
        </span>
        <button type="button" onClick={logout}>
          Logout
        </button>
      </div>
      <App />
    </>
  )
}