import { useEffect, useState } from 'react';
import { api, dateBoundary, localDate } from './api';
import type { Room, User, Reservation, Usage } from './types';
import { Badge, Empty, ErrorBox, formatTime, useApi } from './components/Common';
import BookingForm from './components/BookingForm';
import SchedulePage from './components/SchedulePage';

const displayId = (prefix: string, id: number) => `${prefix}${String(id).padStart(3, '0')}`;

const pages = ['Rooms', 'Users', 'Reservations', 'Schedule', 'Usage'] as const;
type Page = typeof pages[number];
const equipmentOptions = ['Projector', 'TV screen', 'Video conferencing', 'Whiteboard'];
function currentPage(): Page {
  return pages.find(p => p.toLowerCase() === location.hash.slice(1)) ?? 'Rooms';
}

export default function App() {
  const [page, setPage] = useState<Page>(currentPage);
  const [refresh, setRefresh] = useState(0);
  const [editor, setEditor] = useState<{ roomId?: number; reservation?: Reservation } | null>(null);
  const [notice, setNotice] = useState('');
  const rooms = useApi<Room[]>('/rooms', refresh);
  const users = useApi<User[]>('/users', refresh);
  useEffect(() => {
    const change = () => { setPage(currentPage()); setEditor(null); setNotice(''); };
    window.addEventListener('hashchange', change);
    const timer = window.setInterval(() => setRefresh(v => v+1), 60000);
    return () => { window.removeEventListener('hashchange', change); clearInterval(timer); };
  }, []);
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => {
      setPage('Reservations');
      window.location.hash = 'reservations';
      setNotice('');
      window.scrollTo({ top: 0 });
    }, 3000);
    return () => window.clearTimeout(timer);
  }, [notice]);
  function saved() {
    setNotice(editor?.reservation ? 'Reservation updated successfully' : 'Reservation booked successfully');
    setEditor(null);
    setRefresh(v => v+1);
    window.scrollTo({ top: 0 });
  }
  if (notice) return <main className="booking-success-screen" tabIndex={-1} ref={element => element?.focus()} aria-label="Booking confirmation">
    <div role="status" aria-live="polite">
      <span className="booking-confirmation-check" aria-hidden="true">
        <svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="m5 12 4 4 10-10"/></svg>
      </span>
      <h1>{notice}</h1>
    </div>
  </main>;
  return <>
    <header className="app-header">
      <div className="shell header-inner">
        <a className="brand" href="#rooms"><img className="brand-logo" src="/conference-room.svg" alt="Conference Room Booking"/></a>
        <nav className="app-nav" aria-label="Main navigation"><div className="nav-inner">{pages.map(p => <a key={p} href={"#" + p.toLowerCase()} className={page === p ? 'active' : ''} aria-current={page === p ? 'page' : undefined}>{p}</a>)}</div></nav>
        <span className="header-note">Office workspace</span>
      </div>
    </header>
    <main className="shell main-content">
      {!editor && <div className="page-heading"><div><h1>{page}</h1><p>{page === 'Rooms' ? 'Find the right space for your next meeting.' : page === 'Users' ? 'People in the office and their active bookings.' : page === 'Reservations' ? 'Plan meetings and manage existing bookings.' : page === 'Schedule' ? 'See a room’s meetings for one day.' : 'Compare reserved time with recorded room usage.'}</p></div>{!editor && page === 'Reservations' && <button className="btn btn-primary" disabled={!rooms.data || !users.data || !!editor} onClick={() => { setEditor({}); setNotice(''); }}>New reservation</button>}</div>}
      <ErrorBox message={rooms.error || users.error}/>
      {editor && rooms.data && users.data && <BookingForm key={editor.reservation ? `edit-${editor.reservation.id}` : `new-${editor.roomId ?? ''}`} rooms={rooms.data} users={users.data} selectedRoom={editor.roomId} reservation={editor.reservation} onClose={() => setEditor(null)} onSaved={saved}/>}
      {!editor && page === 'Rooms' && <RoomsPage allRooms={rooms.data ?? []} refresh={refresh} onBook={id => { setEditor({ roomId: id }); setNotice(''); window.scrollTo({ top: 0 }); }}/>}
      {!editor && page === 'Users' && <div className="table-panel"><div className="table-responsive" tabIndex={0} role="region" aria-label="Scrollable data table"><table className="table users-table"><thead><tr><th scope="col">ID</th><th scope="col">First name</th><th scope="col">Last name</th><th scope="col" className="cell-number">Active reservations</th></tr></thead><tbody>{users.loading ? <Empty columns={4} text="Loading users…"/> : users.data?.length ? users.data.map(u => <tr key={u.id}><td className="record-id">{displayId('CU', u.id)}</td><td>{u.firstName}</td><td>{u.lastName}</td><td className="cell-number">{u.activeReservations}</td></tr>) : <Empty columns={4}/>}</tbody></table></div><p className="table-note">Active reservations are booked meetings that have not yet ended.</p></div>}
      {!editor && page === 'Reservations' && <ReservationsPage rooms={rooms.data ?? []} users={users.data ?? []} refresh={refresh} onRefresh={() => setRefresh(v => v+1)} onEdit={r => { setEditor({ reservation: r }); setNotice(''); window.scrollTo({ top: 0 }); }}/>}
      {!editor && page === 'Schedule' && <SchedulePage rooms={rooms.data ?? []} refresh={refresh}/>}
      {!editor && page === 'Usage' && <UsagePage refresh={refresh}/>}
    </main>
  </>;
}

function RoomsPage({ allRooms, refresh, onBook }: { allRooms: Room[]; refresh: number; onBook: (id: number) => void }) {
  const [capacity, setCapacity] = useState('');
  const [floor, setFloor] = useState('');
  const [equipment, setEquipment] = useState<string[]>([]);
  const query = new URLSearchParams();
  if (capacity) query.set('minCapacity', capacity);
  if (floor) query.set('floor', floor);
  equipment.forEach(e => query.append('equipment', e));
  const { data, error, loading } = useApi<Room[]>(`/rooms?${query}`, refresh);
  return <>
    <section className="filter-panel room-filters" aria-label="Room filters"><div className="filter-row">
      <label>Minimum capacity<input className="form-control" type="number" min="1" placeholder="Any capacity" value={capacity} onChange={e => setCapacity(e.target.value)}/></label>
      <label>Floor<select className="form-select" value={floor} onChange={e => setFloor(e.target.value)}><option value="">All floors</option>{[...new Set(allRooms.map(r => r.floor))].sort().map(f => <option key={f}>{f}</option>)}</select></label>
      <button className="btn btn-outline-secondary" onClick={() => { setCapacity(''); setFloor(''); setEquipment([]); }}>Clear filters</button>
    </div><fieldset className="equipment-filter"><legend>Equipment <span>(all selected)</span></legend>{equipmentOptions.map(e => <label key={e}><input className="form-check-input" type="checkbox" checked={equipment.includes(e)} onChange={() => setEquipment(old => old.includes(e) ? old.filter(x => x !== e) : [...old, e])}/>{e}</label>)}</fieldset></section>
    <ErrorBox message={error}/><div className="table-panel"><div className="table-responsive" tabIndex={0} role="region" aria-label="Scrollable data table"><table className="table"><thead><tr><th scope="col">Room/ID</th><th scope="col">Capacity</th><th scope="col">Floor</th><th scope="col">Equipment</th><th scope="col">Status</th><th scope="col"><span className="visually-hidden">Actions</span></th></tr></thead><tbody>
      {loading ? <Empty columns={6} text="Loading rooms…"/> : data?.length ? data.map(r => <tr key={r.id}><td><strong>{r.name}</strong><small className="record-id">{displayId('RM', r.id)}</small></td><td>{r.capacity} people</td><td>{r.floor}</td><td><div className="equipment-tags">{r.equipment.map(e => <span key={e}>{e}</span>)}</div></td><td><Badge value={r.isActive ? 'Active' : 'Inactive'}/></td><td className="text-end"><button className="btn btn-sm btn-outline-primary" disabled={!r.isActive} onClick={() => onBook(r.id)}>Book room</button></td></tr>) : <Empty columns={6}/>}
    </tbody></table></div><p className="table-note">Inactive rooms cannot be booked. Availability is checked for your chosen meeting time.</p></div>
  </>;
}

function ReservationsPage({ rooms, users, refresh, onRefresh, onEdit }: { rooms: Room[]; users: User[]; refresh: number; onRefresh: () => void; onEdit: (r: Reservation) => void }) {
  const [room, setRoom] = useState(''); const [organizer, setOrganizer] = useState('');
  const [status, setStatus] = useState(''); const [from, setFrom] = useState(''); const [to, setTo] = useState('');
  const [busy, setBusy] = useState<number | null>(null); const [actionError, setActionError] = useState('');
  const [success, setSuccess] = useState('');
  const query = new URLSearchParams();
  if (room) query.set('roomId', room); if (organizer) query.set('organizerId', organizer); if (status) query.set('status', status);
  if (from) query.set('from', dateBoundary(from)); if (to) query.set('to', dateBoundary(to, true));
  const { data, error, loading } = useApi<Reservation[]>(`/reservations?${query}`, refresh);
  async function act(r: Reservation, action: string) {
    if (action === 'cancel' && !window.confirm(`Cancel “${r.title}”?`)) return;
    setBusy(r.id); setActionError(''); setSuccess('');
    try { await api(`/reservations/${r.id}/${action}`, 'POST'); setSuccess(action === 'cancel' ? 'Reservation cancelled.' : action === 'end' ? 'Meeting ended.' : 'Checked in.'); onRefresh(); }
    catch (e) { setActionError(e instanceof Error ? e.message : 'Action failed.'); }
    finally { setBusy(null); }
  }
  return <>
    <section className="filter-panel filter-row" aria-label="Reservation filters">
      <label>Room<select className="form-select" value={room} onChange={e => setRoom(e.target.value)}><option value="">All rooms</option>{rooms.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
      <label>Organizer<select className="form-select" value={organizer} onChange={e => setOrganizer(e.target.value)}><option value="">All organizers</option>{users.map(u => <option key={u.id} value={u.id}>{u.firstName} {u.lastName}</option>)}</select></label>
      <label>Status<select className="form-select" value={status} onChange={e => setStatus(e.target.value)}><option value="">All statuses</option>{['Booked', 'Completed', 'Cancelled'].map(s => <option key={s}>{s}</option>)}</select></label>
      <label>From<input type="date" className="form-control" value={from} onChange={e => setFrom(e.target.value)}/></label>
      <label>Through<input type="date" className="form-control" value={to} onChange={e => setTo(e.target.value)}/></label>
      <button className="btn btn-outline-secondary" onClick={() => { setRoom(''); setOrganizer(''); setStatus(''); setFrom(''); setTo(''); }}>Clear</button>
    </section>
    <ErrorBox message={actionError || error}/>{success && <p role="status" className="text-success">{success}</p>}
    <div className="table-panel"><div className="table-responsive" tabIndex={0} role="region" aria-label="Scrollable data table"><table className="table reservation-table"><thead><tr><th scope="col">Meeting/ID</th><th scope="col">Room / organizer</th><th scope="col">Start / end</th><th scope="col" className="cell-number">Attendees</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead><tbody>
      {loading ? <Empty columns={6} text="Loading reservations…"/> : data?.length ? data.map(r => <tr key={r.id}><td><strong>{r.title}</strong><small className="record-id">{displayId('RES', r.id)}</small></td><td>{r.room}<small>{r.organizer}</small></td><td className="dates">{formatTime(r.start)}<small>{formatTime(r.end)}</small></td><td className="cell-number">{r.attendeeCount}</td><td><Badge value={r.status}/>{r.checkedInAt && <small>Checked in</small>}</td><td><div className="actions">{r.canEdit && <button className="btn btn-sm btn-outline-secondary" disabled={busy !== null} onClick={() => onEdit(r)}>Edit</button>}{r.canCheckIn && <button className="btn btn-sm btn-outline-primary" disabled={busy !== null} onClick={() => act(r, 'check-in')}>Check in</button>}{r.canEnd && <button className="btn btn-sm btn-outline-primary" disabled={busy !== null} onClick={() => act(r, 'end')}>End meeting</button>}{r.canCancel && <button className="btn btn-sm btn-outline-danger" disabled={busy !== null} onClick={() => act(r, 'cancel')}>Cancel</button>}{!r.canEdit && !r.canCheckIn && !r.canEnd && !r.canCancel && <span className="muted">—</span>}</div></td></tr>) : <Empty columns={6}/>}
    </tbody></table></div><p className="table-note">Check in during the scheduled meeting. End it early to release the room. Dates filter for overlapping meetings.</p></div>
  </>;
}

function UsagePage({ refresh }: { refresh: number }) {
  const [from, setFrom] = useState(() => { const d = new Date(); d.setDate(d.getDate()-30); return localDate(d.toISOString()).slice(0, 10); });
  const [to, setTo] = useState(() => localDate(new Date().toISOString()).slice(0, 10));
  const query = new URLSearchParams();
  if (from) query.set('from', dateBoundary(from)); if (to) query.set('to', dateBoundary(to, true));
  const { data, error, loading } = useApi<Usage[]>(`/usage?${query}`, refresh);
  return <section className="usage-report"><div className="print-heading"><h1>Conference Room Booking</h1><h2>Room usage report</h2><p>{from || 'Last 30 days'} to {to || 'today'} · Times shown in {Intl.DateTimeFormat().resolvedOptions().timeZone}</p></div><section className="filter-panel filter-row" aria-label="Usage date range"><label>From<input className="form-control" type="date" value={from} onChange={e => setFrom(e.target.value)}/></label><label>Through<input className="form-control" type="date" value={to} onChange={e => setTo(e.target.value)}/></label><button type="button" className="btn btn-outline-primary" disabled={loading || !!error || !data?.length} onClick={() => window.print()}>Save as PDF</button><p className="filter-help">Only elapsed time is included. Future time is excluded.</p></section>
    <ErrorBox message={error}/><div className="table-panel"><div className="table-responsive" tabIndex={0} role="region" aria-label="Scrollable data table"><table className="table usage-table"><thead><tr><th scope="col">Room/ID</th><th scope="col" className="cell-number">Bookings</th><th scope="col" className="cell-number">Checked in</th><th scope="col" className="cell-number">No check-in</th><th scope="col" className="cell-number">Booked minutes</th><th scope="col" className="cell-number">Used minutes</th><th scope="col" className="cell-number">Usage</th></tr></thead><tbody>{loading ? <Empty columns={7} text="Loading usage…"/> : data?.length ? data.map(r => <tr key={r.roomId}><td><strong>{r.room}</strong><small className="record-id">{displayId('RM', r.roomId)}</small></td><td className="cell-number">{r.bookings}</td><td className="cell-number">{r.checkedInBookings}</td><td className="cell-number">{r.noShows}</td><td className="cell-number">{r.bookedMinutes}</td><td className="cell-number">{r.usedMinutes}</td><td className="cell-number"><strong>{r.usagePercent}%</strong></td></tr>) : <Empty columns={7}/>}</tbody></table></div></div>
    <aside className="report-notes"><h2>How to read this report</h2><p>Usage is recorded through manual check-in and meeting ending, not room sensors. A low percentage means reserved time has little recorded use.</p><p>Cancelled bookings are excluded. “No check-in” counts meetings that have finished without checking in. Scheduled time stays unchanged when a meeting ends early.</p><p>Minutes are clipped to the selected dates and the current time. Usage = recorded used minutes ÷ booked minutes. If no time was booked, usage is 0%.</p></aside>
  </section>;
}
