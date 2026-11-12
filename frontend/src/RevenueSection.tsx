
import {
  useCallback,
  useEffect,
  useState,
  type FormEvent
} from 'react'

const departments = [
  'TRANSIT',
  'UTIL',
  'RES',
  'SEC',
  'FIN',
  'HR',
  'EMS'
]

const revenueSources = [
  'FINES',
  'FARES',
  'BILLS',
  'FEES'
] as const

type RevenueSource = typeof revenueSources[number]

type RevenueResult = {
  approved: boolean
  message: string
  revenue: {
    id: string
    source: string
    departmentCode: string
    amount: number
    description: string
    createdAtUtc: string
  } | null
}

type RevenueSummary = Record<RevenueSource, number>

const API_URL = '/api/budget/v1/revenue'

const formatCAD = (amount: number) =>
  new Intl.NumberFormat('en-CA', {
    style: 'currency',
    currency: 'CAD'
  }).format(amount)

function RevenueSection() {
  const [source, setSource] = useState<RevenueSource>('FINES')
  const [departmentCode, setDepartmentCode] = useState('SEC')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [result, setResult] = useState<RevenueResult | null>(null)

  const [summary, setSummary] = useState<RevenueSummary>({
    FINES: 0,
    FARES: 0,
    BILLS: 0,
    FEES: 0
  })
  const [summaryError, setSummaryError] = useState('')

  const loadSummary = useCallback(async () => {
    try {
      const response = await fetch(`${API_URL}/summary`)

      if (!response.ok) {
        throw new Error('Unable to load revenue totals.')
      }

      const data: RevenueSummary = await response.json()

      setSummary(data)
      setSummaryError('')
    } catch {
      setSummaryError('Unable to retrieve revenue totals.')
    }
  }, [])

  useEffect(() => {
    void loadSummary()

    const interval = window.setInterval(() => {
      void loadSummary()
    }, 3000)

    return () => window.clearInterval(interval)
  }, [loadSummary])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const revenueAmount = Number(amount)

    if (
      amount.trim() === '' ||
      !Number.isFinite(revenueAmount) ||
      revenueAmount <= 0 ||
      !/^\d+(\.\d{1,2})?$/.test(amount.trim())
    ) {
      setError('Enter a valid amount with at most two decimal places.')
      setResult(null)
      return
    }

    if (!description.trim()) {
      setError('Description is required.')
      setResult(null)
      return
    }

    setLoading(true)
    setError('')
    setResult(null)

    try {
      const response = await fetch(API_URL, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          source,
          departmentCode,
          amount: revenueAmount,
          description: description.trim()
        })
      })

      const data: RevenueResult = await response.json()

      setResult(data)

      if (response.ok && data.approved) {
        setAmount('')
        setDescription('')
        await loadSummary()
      }
    } catch {
      setError('Unable to connect to the revenue API.')
    } finally {
      setLoading(false)
    }
  }

  const grandTotal = revenueSources.reduce(
    (total, currentSource) => total + summary[currentSource],
    0
  )

  return (
    <section aria-labelledby="revenue-heading">
      <h2 id="revenue-heading">BF-15 — Revenue Management</h2>

      <h3>Record City Revenue</h3>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="revenue-source">Revenue Source</label>
          <select
            id="revenue-source"
            value={source}
            onChange={(event) =>
              setSource(event.target.value as RevenueSource)
            }
          >
            {revenueSources.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="revenue-department">Department</label>
          <select
            id="revenue-department"
            value={departmentCode}
            onChange={(event) =>
              setDepartmentCode(event.target.value)
            }
          >
            {departments.map((department) => (
              <option key={department} value={department}>
                {department}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="revenue-amount">Amount (CAD)</label>
          <input
            id="revenue-amount"
            type="number"
            min="0.01"
            step="0.01"
            required
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
          />
        </div>

        <div>
          <label htmlFor="revenue-description">Description</label>
          <input
            id="revenue-description"
            type="text"
            required
            value={description}
            onChange={(event) =>
              setDescription(event.target.value)
            }
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Recording...' : 'Record Revenue'}
        </button>
      </form>

      {error && <p role="alert">Error: {error}</p>}

      {result && (
        <section aria-live="polite">
          <h3>
            {result.approved
              ? 'Revenue Recorded Successfully'
              : 'Revenue Rejected'}
          </h3>
          <p>{result.message}</p>
        </section>
      )}

      <section>
        <h3>Revenue Totals</h3>
        <p>Automatically updates every 3 seconds.</p>

        {summaryError && (
          <p role="alert">{summaryError}</p>
        )}

        <table>
          <thead>
            <tr>
              <th>Revenue Source</th>
              <th>Total (CAD)</th>
            </tr>
          </thead>
          <tbody>
            {revenueSources.map((item) => (
              <tr key={item}>
                <td>{item}</td>
                <td>{formatCAD(summary[item])}</td>
              </tr>
            ))}
            <tr>
              <th scope="row">Grand Total</th>
              <td>{formatCAD(grandTotal)}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </section>
  )
}

export default RevenueSection
