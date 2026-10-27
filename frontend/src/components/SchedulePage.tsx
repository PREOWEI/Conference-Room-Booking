import { useState } from 'react';
import { dateBoundary, localDate } from '../api';
import type { Room, Reservation } from '../types';
import { Badge, ErrorBox, formatTime, useApi } from './Common';

export default function SchedulePage({ rooms, refresh }: { rooms: Room[]; refresh: number }) {
  const [roomId, setRoomId] = useState('1');
  const [date, setDate] = useState(() => localDate(new Date().toISOString()).slice(0, 10));
  const day = date || localDate(new Date().toISOString()).slice(0, 10);
  const query = new URLSearchParams({ from: dateBoundary(day), to: dateBoundary(day, true) });
  const { data, error, loading } = useApi<Reservation[]>(`/rooms/${roomId}/schedule?${query}`, refresh);
  const room = rooms.find(r => r.id === Number(roomId));
  return <>
    <section className="filter-panel filter-row" aria-label="Daily schedule filters">
      <label>Room<select className="form-select" value={roomId} onChange={e => setRoomId(e.target.value)}>{rooms.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
      <label>Date<input className="form-control" type="date" required value={date} onChange={e => setDate(e.target.value)}/></label>
    </section>
    <ErrorBox message={error}/>
    <section className="day-schedule" aria-label="Room daily timeline">
      <div className="schedule-heading"><h2>{room?.name ?? 'Room schedule'}</h2><p>{new Date(day + 'T12:00:00').toLocaleDateString([], { dateStyle: 'long' })}</p></div>
      {room && !room.isActive && <p className="mt-3">This room is inactive and cannot be booked.</p>}
      {loading ? <p className="muted mt-3" role="status">Loading schedule...</p> : error ? null : data?.length ? <ol className="day-timeline">
        {data.map(r => <li key={r.id}>
          <div className="schedule-entry"><div><strong>{r.title}</strong><small>RES{String(r.id).padStart(3, '0')} · {r.organizer}</small></div><Badge value={r.status}/></div>
          <p className="schedule-time">{formatTime(r.start)} – {formatTime(r.end)}</p>
          {r.actualEndAt && <small>Ended at {formatTime(r.actualEndAt)}</small>}
        </li>)}
      </ol> : <p className="muted mt-3">No meetings scheduled for this room on this day.</p>}
      <p className="form-text mb-0">Meetings are shown in time order. Cancelled meetings do not block the room. All times are local.</p>
    </section>
  </>;
}
