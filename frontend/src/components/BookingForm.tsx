import { useState, type FormEvent } from 'react';
import { api, localDate } from '../api';
import type { Booking, Reservation, Room, User } from '../types';
import { ErrorBox } from './Common';

type Errors = Partial<Record<keyof Booking, string>>;
function validate(form: Booking): Errors {
  const errors: Errors = {};
  if (!form.roomId) errors.roomId = 'Please choose a room.';
  if (!form.organizerFirstName.trim()) errors.organizerFirstName = 'Please enter a first name.';
  if (!form.organizerLastName.trim()) errors.organizerLastName = 'Please enter a last name.';
  if (!/^[^\s@]+@[^\s@]+$/.test(form.organizerEmail.trim())) errors.organizerEmail = 'Please enter a valid email address.';
  if (!form.title.trim()) errors.title = 'Please enter a meeting title.';
  if (!Number.isInteger(form.attendeeCount) || form.attendeeCount < 1) errors.attendeeCount = 'Please enter the number of attendees (at least 1).';
  if (!form.start || !Number.isFinite(new Date(form.start).getTime())) errors.start = 'Please choose a start date and time.';
  if (!form.end || !Number.isFinite(new Date(form.end).getTime())) errors.end = 'Please choose an end date and time.';
  return errors;
}

export default function BookingForm({ rooms, users, selectedRoom, reservation, onClose, onSaved }: {
  rooms: Room[]; users: User[]; selectedRoom?: number; reservation?: Reservation;
  onClose: () => void; onSaved: () => void;
}) {
  const [form, setForm] = useState<Booking>({
    roomId: reservation?.roomId ?? selectedRoom ?? 0,
    organizerFirstName: users.find(u => u.id === reservation?.organizerId)?.firstName ?? '',
    organizerLastName: users.find(u => u.id === reservation?.organizerId)?.lastName ?? '',
    organizerEmail: '',
    title: reservation?.title ?? '', attendeeCount: reservation?.attendeeCount ?? 0,
    start: reservation ? localDate(reservation.start) : '', end: reservation ? localDate(reservation.end) : ''
  });
  const [submitted, setSubmitted] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  function change(key: keyof Booking, value: string | number) {
    const next = { ...form, [key]: value };
    setForm(next);
    if (submitted) setErrors(validate(next));
    setError('');
  }
  const field = (key: keyof Booking, select = false) => ({
    className: (select ? 'form-select' : 'form-control') + (errors[key] ? ' is-invalid' : ''),
    'aria-invalid': !!errors[key],
    'aria-describedby': errors[key] ? `error-${key}` : undefined
  });
  const feedback = (key: keyof Booking) => errors[key] ? <div className="invalid-feedback d-block" id={`error-${key}`}>{errors[key]}</div> : null;

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const issues = validate(form);
    setSubmitted(true); setErrors(issues); setError('');
    if (Object.keys(issues).length) {
      const element = e.currentTarget;
      requestAnimationFrame(() => element.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus());
      return;
    }
    setBusy(true);
    try {
      await api(reservation ? `/reservations/${reservation.id}` : '/reservations', reservation ? 'PUT' : 'POST',
        { ...form, start: new Date(form.start).toISOString(), end: new Date(form.end).toISOString() });
      onSaved();
    } catch (e) { setError(e instanceof Error ? e.message : 'Booking failed.'); }
    finally { setBusy(false); }
  }
  return <section className="booking-panel" aria-labelledby="booking-heading">
    <div className="booking-heading">
      <div><h1 id="booking-heading">{reservation ? 'Edit reservation' : 'New reservation'}</h1><p>Reserve a space and set the details for your meeting.</p></div>
    </div>
    <ErrorBox message={error}/>
    <form onSubmit={submit} noValidate>
      <fieldset disabled={busy}>
        <section className="form-section" aria-labelledby="meeting-details">
          <div className="form-section-heading"><h2 id="meeting-details">Meeting details</h2><p>Choose the room, attendees, and meeting time.</p></div>
          <div className="row g-3">
        <div className="col-md-8"><label className="form-label" htmlFor="booking-room">Room</label><select id="booking-room" {...field('roomId', true)} value={form.roomId || ''} onChange={e => change('roomId', +e.target.value)} required><option value="">Choose a room</option>{rooms.filter(r => r.isActive).map(r => <option key={r.id} value={r.id}>{r.name} · {r.capacity} people</option>)}</select>{feedback('roomId')}</div>
        <div className="col-md-4"><label className="form-label" htmlFor="booking-attendees">Attendees</label><input id="booking-attendees" type="number" {...field('attendeeCount')} min="1" step="1" required value={form.attendeeCount || ''} onChange={e => change('attendeeCount', +e.target.value)}/>{feedback('attendeeCount')}<div className="form-text">Include the organizer.</div></div>
        <div className="col-12"><label className="form-label" htmlFor="booking-title">Meeting title</label><input id="booking-title" {...field('title')} required maxLength={120} value={form.title} onChange={e => change('title', e.target.value)}/>{feedback('title')}</div>
        <div className="col-md-6"><label className="form-label" htmlFor="booking-start">Start date and time</label><input id="booking-start" type="datetime-local" {...field('start')} required value={form.start} onChange={e => change('start', e.target.value)}/>{feedback('start')}</div>
        <div className="col-md-6"><label className="form-label" htmlFor="booking-end">End date and time</label><input id="booking-end" type="datetime-local" {...field('end')} required value={form.end} onChange={e => change('end', e.target.value)}/>{feedback('end')}</div>
          </div>
        </section>
        <section className="form-section" aria-labelledby="organizer-details">
          <div className="form-section-heading"><h2 id="organizer-details">Organizer</h2></div>
          <div className="row g-3">
        <div className="col-md-6"><label className="form-label" htmlFor="organizer-first">First name</label><input id="organizer-first" {...field('organizerFirstName')} autoComplete="given-name" maxLength={80} required value={form.organizerFirstName} onChange={e => change('organizerFirstName', e.target.value)}/>{feedback('organizerFirstName')}</div>
        <div className="col-md-6"><label className="form-label" htmlFor="organizer-last">Last name</label><input id="organizer-last" {...field('organizerLastName')} autoComplete="family-name" maxLength={80} required value={form.organizerLastName} onChange={e => change('organizerLastName', e.target.value)}/>{feedback('organizerLastName')}</div>
        <div className="col-12"><label className="form-label" htmlFor="organizer-email">Email</label><input id="organizer-email" type="email" {...field('organizerEmail')} autoComplete="email" maxLength={254} required value={form.organizerEmail} onChange={e => change('organizerEmail', e.target.value)}/>{feedback('organizerEmail')}</div>
          </div>
        </section>
        <div className="booking-actions">
          <p>Dates and times use your local timezone.</p>
          <div className="booking-actions-buttons">
            <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary" type="submit">{busy ? 'Saving...' : reservation ? 'Save changes' : 'Confirm reservation'}</button>
          </div>
        </div>
      </fieldset>
    </form>
  </section>;
}
