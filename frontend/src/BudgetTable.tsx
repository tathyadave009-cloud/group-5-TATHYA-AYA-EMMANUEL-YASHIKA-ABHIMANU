import { useEffect, useState } from 'react';

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

const REFRESH_MS = 3000;

const money = (n: number) =>
  n.toLocaleString('en-CA', { style: 'currency', currency: 'CAD' });

export default function BudgetTable() {
  const [budgets, setBudgets] = useState<DepartmentBudget[]>([]);
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let active = true; // becomes false when the page closes, so we stop updating

    async function load() {
      try {
        const res = await fetch('/api/budgets');
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
  }, []);

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
          </tr>
        </thead>
        <tbody>
          {budgets.map((b) => {
            const code = b.departmentCode ?? b.departmentId ?? '';
            return (
              <tr key={code || b.departmentName}>
                <td>
                  {code} – {b.departmentName}
                </td>
                <td>{money(b.allocated)}</td>
                <td>{money(b.spent)}</td>
                <td>{money(b.remaining)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}