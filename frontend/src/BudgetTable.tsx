import { Fragment, useEffect, useState } from 'react';

// One department, exactly as GET /api/budgets returns it.
// The code field is called departmentCode in Aya's message and departmentId in
// the sprint doc, so we accept either one.
interface DepartmentBudget {
  departmentCode?: string;
  departmentId?: string;
  departmentName: string;
  allocated: number;
  spent: number;
  remaining: number;
}

// The address of the budgets API. After Tathya's routes pull request is merged,
// change this ONE line to '/api/budget/v1/budgets' (BF-11).
const BUDGETS_URL = '/api/budget/v1/budgets';

const REFRESH_MS = 3000;

const money = (n: number) =>
  n.toLocaleString('en-CA', { style: 'currency', currency: 'CAD' });

// Pulls the "message" out of the class error format { error, message, traceId }.
function messageFrom(body: unknown, fallback: string): string {
  if (body && typeof body === 'object' && 'message' in body) {
    const m = (body as { message: unknown }).message;
    if (typeof m === 'string' && m) return m;
  }
  return fallback;
}

export default function BudgetTable() {
  const [budgets, setBudgets] = useState<DepartmentBudget[]>([]);
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState(false);

  // BF-16: which row is being edited (null = none), what is typed in the box,
  // whether a save is in progress, and the red error text.
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  useEffect(() => {
    // While someone is typing a new budget we do NOT refresh, or the box would
    // be wiped every 3 seconds. When editing ends, this effect runs again, loads
    // fresh numbers right away and restarts the 3-second timer.
    if (editingCode !== null) return;

    let active = true; // becomes false when the page closes, so we stop updating

    async function load() {
      try {
        const res = await fetch(BUDGETS_URL);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data: unknown = await res.json();
        if (!Array.isArray(data)) throw new Error('Unexpected response');
        if (active) {
          setBudgets(data as DepartmentBudget[]);
          setError('');
          setLoaded(true);
        }
      } catch {
        if (active) setError('Budget & Finance service is unavailable.');
      }
    }

    load(); // first load right away
    const timer = setInterval(load, REFRESH_MS); // then every 3 seconds

    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [editingCode]);

  function startEdit(code: string, allocated: number) {
    setEditingCode(code);
    setDraft(String(allocated));
    setSaveError('');
  }

  function cancelEdit() {
    setEditingCode(null);
    setSaveError('');
  }

  async function saveEdit(code: string) {
    const value = Number(draft);
    // Check on the page first: a number above 0 with at most 2 decimals.
    if (!Number.isFinite(value) || value <= 0 || Math.round(value * 100) / 100 !== value) {
      setSaveError('Enter a budget above 0 with at most 2 decimals.');
      return;
    }

    setSaving(true);
    setSaveError('');
    try {
      const res = await fetch(`${BUDGETS_URL}/${encodeURIComponent(code)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ allocated: value }),
      });
      if (!res.ok) {
        // The server said no (for example: lower than already spent).
        const body: unknown = await res.json().catch(() => null);
        setSaveError(messageFrom(body, `Could not save the budget (HTTP ${res.status}).`));
        return;
      }
      setEditingCode(null); // success: close the box, the table reloads by itself
    } catch {
      setSaveError('Budget & Finance service is unavailable.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <section>
      <h2>Department Budgets</h2>
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loaded && !error && <p>Loading budgets...</p>}

      <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th>Department</th>
            <th>Allocated</th>
            <th>Spent</th>
            <th>Remaining</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {budgets.map((b) => {
            const code = b.departmentCode ?? b.departmentId ?? '';
            const isEditing = editingCode === code;
            return (
              <Fragment key={code || b.departmentName}>
                <tr>
                  <td>
                    {code} – {b.departmentName}
                  </td>
                  <td>
                    {isEditing ? (
                      <input
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={draft}
                        aria-label={`New budget for ${code}`}
                        disabled={saving}
                        onChange={(e) => setDraft(e.target.value)}
                      />
                    ) : (
                      money(b.allocated)
                    )}
                  </td>
                  <td>{money(b.spent)}</td>
                  <td>{money(b.remaining)}</td>
                  <td>
                    {isEditing ? (
                      <>
                        <button type="button" disabled={saving} onClick={() => saveEdit(code)}>
                          {saving ? 'Saving...' : 'Save'}
                        </button>{' '}
                        <button type="button" disabled={saving} onClick={cancelEdit}>
                          Cancel
                        </button>
                      </>
                    ) : (
                      <button
                        type="button"
                        disabled={editingCode !== null}
                        onClick={() => startEdit(code, b.allocated)}
                      >
                        Edit
                      </button>
                    )}
                  </td>
                </tr>
                {isEditing && saveError && (
                  <tr>
                    <td colSpan={5} style={{ color: 'red' }} role="alert">
                      {saveError}
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}