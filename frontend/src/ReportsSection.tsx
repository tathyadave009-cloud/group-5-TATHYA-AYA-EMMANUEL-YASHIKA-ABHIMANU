import { useState, type FormEvent } from 'react'
import './ReportsSection.css'

type ReportSummary = {
  from: string
  to: string
  spentByDepartment: Record<string, number>
  revenueBySource: Record<string, number>
  totalSpent: number
  totalRevenue: number
  net: number
  currency: string
}

const API_URL = '/api/budget/v1/reports/summary'

const formatCAD = (amount: number) =>
  new Intl.NumberFormat('en-CA', {
    style: 'currency',
    currency: 'CAD'
  }).format(amount)

function ReportsSection() {
  const today = new Date().toISOString().slice(0, 10)

  const [from, setFrom] = useState(
    `${today.slice(0, 7)}-01`
  )
  const [to, setTo] = useState(today)

  const [report, setReport] = useState<ReportSummary | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  // Retrieve the report for the selected date range.
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    setLoading(true)
    setError('')
    setReport(null)

    try {
      const parameters = new URLSearchParams({ from, to })

      const response = await fetch(
        `${API_URL}?${parameters.toString()}`
      )

      if (!response.ok) {
        const apiError: { message?: string } = await response.json()
        setError(apiError.message || 'Unable to retrieve report.')
        return
      }

      const data: ReportSummary = await response.json()
      setReport(data)
    } catch {
      setError('Unable to connect to the reports API.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="reports-section" aria-labelledby="reports-heading">
      <h2 id="reports-heading">BF-26 — Financial Reports</h2>

      <p>Select a date range to view spending and revenue.</p>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="report-from">From</label>
          <input
            id="report-from"
            type="date"
            required
            value={from}
            onChange={(event) => setFrom(event.target.value)}
          />
        </div>

        <div>
          <label htmlFor="report-to">To</label>
          <input
            id="report-to"
            type="date"
            required
            value={to}
            onChange={(event) => setTo(event.target.value)}
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Loading...' : 'Show'}
        </button>
      </form>

      {error && <p role="alert">Error: {error}</p>}

      {report && (
        <section aria-live="polite">
          <p>
            Report period: {report.from.slice(0, 10)} to{' '}
            {report.to.slice(0, 10)}
          </p>

          <h3>Spending by Department</h3>

          <table>
            <thead>
              <tr>
                <th>Department</th>
                <th>Amount (CAD)</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(report.spentByDepartment).map(
                ([department, amount]) => (
                  <tr key={department}>
                    <td>{department}</td>
                    <td>{formatCAD(amount)}</td>
                  </tr>
                )
              )}
              <tr>
                <th scope="row">Total Spending</th>
                <td>{formatCAD(report.totalSpent)}</td>
              </tr>
            </tbody>
          </table>

          <h3>Revenue by Source</h3>

          <table>
            <thead>
              <tr>
                <th>Revenue Source</th>
                <th>Amount (CAD)</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(report.revenueBySource).map(
                ([source, amount]) => (
                  <tr key={source}>
                    <td>{source}</td>
                    <td>{formatCAD(amount)}</td>
                  </tr>
                )
              )}
              <tr>
                <th scope="row">Total Revenue</th>
                <td>{formatCAD(report.totalRevenue)}</td>
              </tr>
            </tbody>
          </table>

          <h3>Net Balance</h3>
          <p>
            <strong>{formatCAD(report.net)} CAD</strong>
          </p>
        </section>
      )}
    </section>
  )
}

export default ReportsSection
