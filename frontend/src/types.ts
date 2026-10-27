export type Room = { id: number; name: string; capacity: number; floor: string; equipment: string[]; isActive: boolean };
export type User = { id: number; firstName: string; lastName: string; activeReservations: number };
export type Reservation = {
  id: number; roomId: number; room: string; organizerId: number; organizer: string;
  title: string; start: string; end: string; attendeeCount: number;
  status: 'Booked' | 'Completed' | 'Cancelled'; checkedInAt: string | null; actualEndAt: string | null;
  canEdit: boolean; canCancel: boolean; canCheckIn: boolean; canEnd: boolean;
};
export type Booking = { roomId: number; organizerFirstName: string; organizerLastName: string; organizerEmail: string; title: string; start: string; end: string; attendeeCount: number };
export type Usage = { roomId: number; room: string; bookings: number; checkedInBookings: number; noShows: number; bookedMinutes: number; usedMinutes: number; usagePercent: number };
