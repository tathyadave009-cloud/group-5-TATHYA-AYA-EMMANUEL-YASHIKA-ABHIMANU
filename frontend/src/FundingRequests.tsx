import { useEffect, useState } from 'react';

// One funding request, exactly as GET /api/budget/v1/funding-requests returns it.
// The server sends status as a number (0 Pending, 1 Approved, 2 Rejected), but we
// also accept the words in case the team changes the API to send text later.
interface FundingRequest {
  id: string;
  departmentCode: string;
  amount: number;
  reason: string;
  urgent: boolean;
  createdAtUtc: string;
  status: number | string;
  decidedAtUtc: string | null;
}

const FUNDING_URL = '/api/budget/v1/funding-requests';
const REFRESH_MS = 3000;

const money = (n: number) =>
  n.toLocaleString('en-CA', { style: 'currency', currency: 'CAD' });

// Turns the status from the API (number or text) into the word we show.
function statusText(status: number | string): string {
  if (status === 0 || status === 'Pending') return 'Pending';
  if (status === 1 || status === 'Approved') return 'Approved';
  if (status === 2 || status === 'Rejected') return 'Rejected';
  return String(status);
}

// Pulls the "message" out of an error body such as { message: "..." }.
function messageFrom(body: unknown, fallback: string): string {
  if (body && typeof body === 'object' && 'message' in body) {
    const m = (body as { message: unknown }).message;
    if (typeof m === 'string' && m) return m;
  }
  return fallback;
}

export default function FundingRequests() {
  const [requests, setRequests] = useState<FundingRequest[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState('');
  const [busyId, setBusyId] = useState<string | null>(null); // request being decided
  const [actionError, setActionError] = useState('');

  async function load(isActive: () => boolean = () => true) {
    try {
      const res = await fetch(FUNDING_URL);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: unknown = await res.json();
      if (!Array.isArray(data)) throw new Error('Unexpected response');
      if (isActive()) {
        setRequests(data as FundingRequest[]);
        setError('');
        setLoaded(true);
      }
    } catch {
      if (isActive()) setError('Budget & Finance service is unavailable.');
    }
  }

  useEffect(() => {
    let active = true; // becomes false when the page closes, so we stop updating
    const isActive = () => active;

    load(isActive); // first load right away
    const timer = setInterval(() => load(isActive), REFRESH_MS); // then every 3 seconds

    return () => {
      active = false;
      clearInterval(timer);
    };
  }, []);

  async function decide(id: string, approved: boolean) {
    setBusyId(id);
    setActionError('');
    try {
      const res = await fetch(`${FUNDING_URL}/${encodeURIComponent(id)}/decision`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ approved }),
      });
      if (!res.ok) {
        // For example 409: someone already decided this request.
        const body: unknown = await res.json().catch(() => null);
        setActionError(messageFrom(body, `Could not save the decision (HTTP ${res.status}).`));
      }
      await load(); // show the new status right away (the budget table refreshes by itself)
    } catch {
      setActionError('Budget & Finance service is unavailable.');
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section>
      <h2>Funding Requests</h2>
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {actionError && (
        <p style={{ color: 'red' }} role="alert">
          {actionError}
        </p>
      )}
      {!loaded && !error && <p>Loading funding requests...</p>}
      {loaded && requests.length === 0 && <p>No funding requests yet.</p>}

      {requests.length > 0 && (
        <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th>Department</th>
              <th>Amount</th>
              <th>Reason</th>
              <th>Urgent</th>
              <th>Status</th>
              <th>Date</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {requests.map((r) => {
              const status = statusText(r.status);
              return (
                <tr key={r.id}>
                  <td>{r.departmentCode}</td>
                  <td>{money(r.amount)}</td>
                  <td>{r.reason}</td>
                  <td>
                    {r.urgent && (
                      <span
                        style={{
                          background: 'crimson',
                          color: 'white',
                          padding: '2px 6px',
                          borderRadius: 4,
                          fontSize: '0.8em',
                        }}
                      >
                        URGENT
                      </span>
                    )}
                  </td>
                  <td>{status}</td>
                  <td>{new Date(r.createdAtUtc).toLocaleString()}</td>
                  <td>
                    {status === 'Pending' && (
                      <>
                        <button
                          type="button"
                          disabled={busyId !== null}
                          onClick={() => decide(r.id, true)}
                        >
                          Approve
                        </button>{' '}
                        <button
                          type="button"
                          disabled={busyId !== null}
                          onClick={() => decide(r.id, false)}
                        >
                          Reject
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}