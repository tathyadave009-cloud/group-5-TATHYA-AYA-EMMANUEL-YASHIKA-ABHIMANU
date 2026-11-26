import { useEffect, useState } from 'react';

// BF-30 (Emmanuel's API): our API asks HR for headcount and salary totals per
// department. HR never shares individual salaries, only these totals.
const PAYROLL_URL = '/api/budget/v1/hr/payroll';
const REFRESH_MS = 10000; // HR is another service, so we ask less often

const money = (n: number) =>
  n.toLocaleString('en-CA', { style: 'currency', currency: 'CAD' });

type Row = Record<string, unknown>;

// Takes the first field that exists, because the exact names come from HR's service.
function pick(row: Row, keys: string[]): unknown {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return row[key];
  }
  return undefined;
}

// The answer may be a plain list or an object that holds the list.
function toRows(data: unknown): Row[] {
  if (Array.isArray(data)) return data as Row[];
  if (data && typeof data === 'object') {
    const obj = data as Record<string, unknown>;
    for (const key of ['departments', 'items', 'data', 'payroll']) {
      if (Array.isArray(obj[key])) return obj[key] as Row[];
    }
  }
  return [];
}

// Pulls the "message" out of the class error format { error, message, traceId }.
function messageFrom(body: unknown, fallback: string): string {
  if (body && typeof body === 'object' && 'message' in body) {
    const m = (body as { message: unknown }).message;
    if (typeof m === 'string' && m) return m;
  }
  return fallback;
}

export default function HrPayroll() {
  const [rows, setRows] = useState<Row[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true; // becomes false when the page closes, so we stop updating

    async function load() {
      try {
        const res = await fetch(PAYROLL_URL);
        if (!res.ok) {
          // 503 means HR is down. Show our API's message if it sent one.
          const body: unknown = await res.json().catch(() => null);
          if (active) setError(messageFrom(body, 'HR service unavailable.'));
          return;
        }
        const data: unknown = await res.json();
        if (active) {
          setRows(toRows(data));
          setError('');
          setLoaded(true);
        }
      } catch {
        if (active) setError('HR service unavailable.');
      }
    }

    load(); // first load right away
    const timer = setInterval(load, REFRESH_MS);

    return () => {
      active = false;
      clearInterval(timer);
    };
  }, []);

  return (
    <section>
      <h2>HR Payroll</h2>
      {error && (
        <p style={{ color: 'red' }} role="alert">
          {error}
        </p>
      )}
      {!loaded && !error && <p>Loading HR payroll...</p>}
      {loaded && !error && rows.length === 0 && <p>HR has no payroll data yet.</p>}

      {rows.length > 0 && (
        <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th>Department</th>
              <th>Headcount</th>
              <th>Salary cost</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => {
              const department = pick(row, [
                'departmentCode',
                'department',
                'departmentId',
                'departmentName',
                'code',
              ]);
              const headcount = pick(row, [
                'headcount',
                'headCount',
                'employeeCount',
                'employees',
              ]);
              const salary = pick(row, [
                'salaryCost',
                'totalSalaryCost',
                'totalSalary',
                'salaryTotal',
                'totalPayroll',
                'payroll',
              ]);
              return (
                <tr key={String(department ?? index)}>
                  <td>{department !== undefined ? String(department) : '—'}</td>
                  <td>{headcount !== undefined ? String(headcount) : '—'}</td>
                  <td>{typeof salary === 'number' ? money(salary) : '—'}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}