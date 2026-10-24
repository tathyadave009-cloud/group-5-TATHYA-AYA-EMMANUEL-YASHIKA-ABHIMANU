import { useEffect, useState } from 'react'
import './App.css'
import BudgetTable from './BudgetTable'

const departments = [
  'TRANSIT',
  'UTIL',
  'RES',
  'SEC',
  'FIN',
  'HR',
  'EMS'
]

type ExpenseResult = {
  approved?: boolean
  message?: string
  remainingBudget?: number
}

type Expense = {
  id: string
  departmentCode: string
  amount: number
  description: string
  createdAtUtc: string
}

function App() {
  const [departmentCode, setDepartmentCode] = useState('EMS')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [result, setResult] = useState<ExpenseResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [expenses, setExpenses] = useState<Expense[]>([])
  const [expensesError, setExpensesError] = useState('')

  async function loadExpenses() {
    try {
      const response = await fetch('/api/expenses')

      if (!response.ok) {
        throw new Error('Could not load expenses.')
      }

      const data: Expense[] = await response.json()
      setExpenses(data)
      setExpensesError('')
    } catch {
      setExpensesError('Unable to retrieve expenses from the API.')
    }
  }

  useEffect(() => {
    void loadExpenses()

    const interval = window.setInterval(() => {
      void loadExpenses()
    }, 3000)

    return () => window.clearInterval(interval)
  }, [])

  async function handleSubmit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()

    const expenseAmount = Number(amount)

    if (!Number.isFinite(expenseAmount) || expenseAmount <= 0) {
      setError('Please enter a valid expense amount.')
      return
    }

    setLoading(true)
    setError('')
    setResult(null)

    try {
      const response = await fetch('/api/expenses', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          departmentCode,
          amount: expenseAmount,
          description
        })
      })

      const data: ExpenseResult = await response.json()

      if (!response.ok && !data.message) {
        throw new Error('Expense request failed.')
      }

      setResult(data)

      if (response.ok) {
        setAmount('')
        setDescription('')
        await loadExpenses()
      }
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to connect to the API.'
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <main>
      <h1>Budget & Finance Service</h1>

      <BudgetTable />

      <h2>BF-05 — Expense Submission</h2>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="department">Department</label>
          <select
            id="department"
            value={departmentCode}
            onChange={(event) => setDepartmentCode(event.target.value)}
          >
            {departments.map((department) => (
              <option key={department} value={department}>
                {department}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="amount">Expense Amount ($)</label>
          <input
            id="amount"
            type="number"
            min="0.01"
            step="0.01"
            required
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
          />
        </div>

        <div>
          <label htmlFor="description">Description</label>
          <input
            id="description"
            type="text"
            required
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Submitting...' : 'Submit Expense'}
        </button>
      </form>

      {error && <p role="alert">Error: {error}</p>}

      {result && (
        <section aria-live="polite">
          <h3>{result.approved ? 'Expense Approved' : 'Expense Rejected'}</h3>
          <p>{result.message}</p>
          {result.remainingBudget !== undefined && (
            <p>Remaining Budget: ${result.remainingBudget.toFixed(2)}</p>
          )}
        </section>
      )}

      <section>
        <h2>Recorded Expenses</h2>
        <p>Automatically updates every 3 seconds.</p>

        {expensesError && <p role="alert">{expensesError}</p>}

        {expenses.length === 0 ? (
          <p>No expenses recorded yet.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Department</th>
                <th>Amount</th>
                <th>Description</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              {expenses.map((expense) => (
                <tr key={expense.id}>
                  <td>{expense.departmentCode}</td>
                  <td>${expense.amount.toFixed(2)}</td>
                  <td>{expense.description}</td>
                  <td>
                    {new Date(expense.createdAtUtc).toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </main>
  )
}

export default App