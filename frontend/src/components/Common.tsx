import { useEffect, useState } from 'react';
import { api } from '../api';

export function useApi<T>(path: string, refresh = 0) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    api<T>(path, 'GET', undefined, controller.signal).then(setData).catch(e => {
      if (!controller.signal.aborted) setError(e.message || 'Cannot reach the server. Check that the backend is running.');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [path, refresh]);
  return { data, error, loading };
}
export function ErrorBox({ message }: { message: string }) {
  return message ? <div role="alert" className="alert alert-danger">{message}</div> : null;
}
export function Badge({ value }: { value: string }) {
  return <span className={`status status-${value.toLowerCase()}`}>{value}</span>;
}
export function Empty({ columns, text = 'No results match these filters.' }: { columns: number; text?: string }) {
  const loading = text.startsWith('Loading');
  return <tr><td colSpan={columns} className="empty"><div className="empty-content" role="status">
    <strong>{loading ? text : 'No results found'}</strong>
    {!loading && <span>{text}</span>}
  </div></td></tr>;
}
export const formatTime = (value: string) => new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
