import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readFileSync } from 'node:fs';

// Follow the existing .NET development URL.
const settings = JSON.parse(readFileSync(new URL('../backend/MeetingRoomBooking.Api/Properties/launchSettings.json', import.meta.url), 'utf8').replace(/^\uFEFF/, ''));
export default defineConfig({ plugins: [react()], server: { proxy: { '/api': settings.profiles.http.applicationUrl.split(';')[0] } } });
