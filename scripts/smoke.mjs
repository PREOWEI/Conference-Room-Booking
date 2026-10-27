import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const settings = JSON.parse(readFileSync(new URL('../backend/MeetingRoomBooking.Api/Properties/launchSettings.json', import.meta.url), 'utf8').replace(/^\uFEFF/, ''));
const base = settings.profiles.http.applicationUrl.split(';')[0];
let checks = 0;
async function call(path, method = 'GET', body, expected = 200) {
  const response = await fetch(`${base}/api${path}`, { method, headers: { 'Content-Type': 'application/json' }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  assert.equal(response.status, expected, `${method} ${path}: ${text}`); checks++;
  return text ? JSON.parse(text) : null;
}
const rooms = await call('/rooms'); assert.equal(rooms.length, 5);
const filtered = await call('/rooms?minCapacity=8&equipment=Whiteboard&floor=2'); assert.deepEqual(filtered.map(r => r.name), ['Conference Room 3']);
await call('/users'); await call('/reservations'); await call('/usage');
const start = new Date(Date.now() + 10 * 86400000).toISOString();
const end = new Date(Date.parse(start) + 3600000).toISOString();
const booking = { roomId: 1, organizerId: 1, title: 'API verification', start, end, attendeeCount: 4 };
const created = await call('/reservations', 'POST', booking, 201);
try {
  assert.equal((await call('/reservations', 'POST', { ...booking, organizerId: 2 }, 409)).code, 'ROOM_BOOKING_CONFLICT');
  assert.equal((await call('/reservations', 'POST', { ...booking, roomId: 2 }, 409)).code, 'ORGANIZER_BOOKING_CONFLICT');
  assert.equal((await call('/reservations', 'POST', { ...booking, roomId: 5 }, 400)).code, 'ROOM_INACTIVE');
  assert.equal((await call('/reservations', 'POST', { ...booking, attendeeCount: 20 }, 400)).code, 'CAPACITY_EXCEEDED');
  await call(`/reservations/${created.id}`, 'PUT', { ...booking, title: 'Edited verification' }, 204);
  const result = await call('/reservations?roomId=1&organizerId=1&status=Booked');
  assert.ok(result.some(r => r.id === created.id && r.title === 'Edited verification'));
  await call(`/reservations/${created.id}/check-in`, 'POST', undefined, 409);
} finally { await call(`/reservations/${created.id}/cancel`, 'POST', undefined, 204); }
const replacement = await call('/reservations', 'POST', booking, 201);
await call(`/reservations/${replacement.id}/cancel`, 'POST', undefined, 204);
const ongoing = (await call('/reservations')).find(r => r.title === 'Ready for check-in' && r.canCheckIn);
if (ongoing) {
  await call(`/reservations/${ongoing.id}/check-in`, 'POST', undefined, 204);
  await call(`/reservations/${ongoing.id}/end`, 'POST', undefined, 204);
  const ended = (await call('/reservations')).find(r => r.id === ongoing.id);
  assert.equal(ended.status, 'Completed'); assert.ok(ended.actualEndAt); assert.equal(ended.end, ongoing.end);
}
await call('/usage');
console.log(`${checks} HTTP checks passed. Test bookings are cancelled; restart the backend to reset sample data.`);
