export async function api<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`/api${path}`, {
    method, signal, headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const errors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new Error(errors || problem?.detail || problem?.title || `Request failed (${response.status}).`);
  }
  return response.status === 204 ? undefined as T : response.json();
}
export function localDate(value: string) {
  const d = new Date(value);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
export function dateBoundary(date: string, end = false) {
  const d = new Date(`${date}T00:00:00`);
  if (end) d.setDate(d.getDate()+1);
  return d.toISOString();
}
