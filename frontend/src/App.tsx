import {
  useCallback,
  useEffect,
  useState,
  type FormEvent
} from 'react'
import './App.css'
import BudgetTable from './BudgetTable'
import RevenueSection from './RevenueSection'
import FundingRequests from './FundingRequests.tsx'

const departments = [
  'TRANSIT',
  'UTIL',
  'RES',
  'SEC',
  'FIN',
  'HR',
  'EMS'
]

const PAGE_SIZE = 25

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

type ExpensePageResponse = {
  items: Expense[]
  page: number
  pageSize: number
  totalCount: number
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
  const [expenseFilter, setExpenseFilter] = useState('')
  const [expensePage, setExpensePage] = useState(1)
  const [totalExpenses, setTotalExpenses] = useState(0)

  const loadExpenses = useCallback(
    async (
      selectedDepartment: string,
      selectedPage: number
    ) => {
      try {
        const parameters = new URLSearchParams({
          page: selectedPage.toString(),
          pageSize: PAGE_SIZE.toString()
        })

        if (selectedDepartment) {
          parameters.set('department', selectedDepartment)
        }

        const response = await fetch(
          `/api/budget/v1/expenses?${parameters.toString()}`
        )

        if (!response.ok) {
          throw new Error('Could not load expenses.')
        }

        const data: ExpensePageResponse =
          await response.json()

        setExpenses(data.items)
        setTotalExpenses(data.totalCount)
        setExpensesError('')
      } catch {
        setExpensesError(
          'Unable to retrieve expenses from the API.'
        )
      }
    },
    []
  )

  useEffect(() => {
    void loadExpenses(expenseFilter, expensePage)

    const interval = window.setInterval(() => {
      void loadExpenses(expenseFilter, expensePage)
    }, 3000)

    return () => window.clearInterval(interval)
  }, [expenseFilter, expensePage, loadExpenses])

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault()

    const expenseAmount = Number(amount)

    if (
      !Number.isFinite(expenseAmount) ||
      expenseAmount <= 0
    ) {
      setError('Please enter a valid expense amount.')
      return
    }

    setLoading(true)
    setError('')
    setResult(null)

    try {
      const response = await fetch('/api/budget/v1/expenses', {
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
        setExpensePage(1)
        await loadExpenses(expenseFilter, 1)
      }
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : 'Unable to connect to the API.'
      )
    } finally {
      setLoading(false)
    }
  }

  const totalPages = Math.max(
    1,
    Math.ceil(totalExpenses / PAGE_SIZE)
  )

  return (
    <main>
      <h1>Budget & Finance Service</h1>

      <BudgetTable />

      <FundingRequests />

      <h2>BF-05 — Expense Submission</h2>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="department">Department</label>
          <select
            id="department"
            value={departmentCode}
            onChange={(event) =>
              setDepartmentCode(event.target.value)
            }
          >
            {departments.map((department) => (
              <option
                key={department}
                value={department}
              >
                {department}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="amount">
            Expense Amount ($)
          </label>
          <input
            id="amount"
            type="number"
            min="0.01"
            step="0.01"
            required
            value={amount}
            onChange={(event) =>
              setAmount(event.target.value)
            }
          />
        </div>

        <div>
          <label htmlFor="description">
            Description
          </label>
          <input
            id="description"
            type="text"
            required
            value={description}
            onChange={(event) =>
              setDescription(event.target.value)
            }
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading
            ? 'Submitting...'
            : 'Submit Expense'}
        </button>
      </form>

      {error && <p role="alert">Error: {error}</p>}

      {result && (
        <section aria-live="polite">
          <h3>
            {result.approved
              ? 'Expense Approved'
              : 'Expense Rejected'}
          </h3>

          <p>{result.message}</p>

          {result.remainingBudget != null && (
            <p>
              Remaining Budget: $
              {result.remainingBudget.toFixed(2)}
            </p>
          )}
        </section>
      )}

      <section>
        <h2>Recorded Expenses</h2>
        <p>Automatically updates every 3 seconds.</p>

        <div>
          <label htmlFor="expense-filter">
            Filter by department
          </label>

          <select
            id="expense-filter"
            value={expenseFilter}
            onChange={(event) => {
              setExpenseFilter(event.target.value)
              setExpensePage(1)
            }}
          >
            <option value="">All departments</option>

            {departments.map((department) => (
              <option
                key={department}
                value={department}
              >
                {department}
              </option>
            ))}
          </select>
        </div>

        {expensesError && (
          <p role="alert">{expensesError}</p>
        )}

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
                    {new Date(
                      expense.createdAtUtc
                    ).toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <div>
          <button
            type="button"
            disabled={expensePage <= 1}
            onClick={() =>
              setExpensePage((current) =>
                Math.max(1, current - 1)
              )
            }
          >
            Previous
          </button>

          <span>
            {' '}
            Page {expensePage} of {totalPages}{' '}
          </span>

          <button
            type="button"
            disabled={expensePage >= totalPages}
            onClick={() =>
              setExpensePage((current) =>
                Math.min(totalPages, current + 1)
              )
            }
          >
            Next
          </button>

          <p>Total expenses: {totalExpenses}</p>
        </div>
           </section>

      <RevenueSection />
    </main>
  )
}

export default App