import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isKnownPagePath, previewFormError } from '../src/lib/preview.ts';

const form = values => {
  const data = new FormData();
  for (const [key, value] of Object.entries(values)) data.set(key, value);
  return data;
};

test('all supplied screen routes and supported edit paths resolve', () => {
  for (const prefix of ['preview', 'admin']) {
    for (const suffix of [
      'dashboard', 'students', 'students/new', 'students/HS0261', 'students/HS0261/edit',
      'teachers', 'teachers/new', 'teachers/GV001', 'teachers/GV001/edit',
      'classes', 'classes/new', 'classes/1-1', 'classes/1-1/edit',
      'attendance', 'events', 'events/new', 'events/nature', 'events/nature/edit',
      'announcements', 'announcements/new', 'announcements/menu', 'announcements/menu/edit',
      'fees', 'fees/HS0261', 'reports', 'settings', 'settings/years', 'permissions', 'help'
    ]) assert.equal(isKnownPagePath(`/${prefix}/${suffix}`), true, suffix);
  }
});

test('unknown paths do not silently display another screen', () => {
  for (const path of ['/preview/unknown', '/preview/events/nature/delete', '/preview/students/new/edit', '/preview/attendance/HS0261', '/preview/settings/unknown', '/admin/classes/id/edit/extra', '/other/dashboard']) {
    assert.equal(isKnownPagePath(path), false, path);
  }
});

test('date range and same-day event times are validated', () => {
  assert.match(previewFormError(form({ startDate: '2026-11-13', endDate: '2026-11-12' })), /Ngày kết thúc/);
  for (const endTime of ['08:00', '08:30']) {
    assert.match(previewFormError(form({ startDate: '2026-11-13', endDate: '2026-11-13', startTime: '08:30', endTime })), /Giờ kết thúc/);
  }
  assert.equal(previewFormError(form({ startDate: '2026-11-13', endDate: '2026-11-13', startTime: '08:30', endTime: '10:00' })), undefined);
  assert.equal(previewFormError(form({ startDate: '2026-11-13', endDate: '2026-11-14', startTime: '23:00', endTime: '01:00' })), undefined);
});

test('scheduling requires both date and time but immediate send does not', () => {
  for (const values of [{}, { scheduledDate: '2026-11-13' }, { scheduledTime: '08:30' }]) {
    assert.match(previewFormError(form({ send: 'Lên lịch gửi', ...values })), /ngày và giờ/);
  }
  assert.equal(previewFormError(form({ send: 'Lên lịch gửi', scheduledDate: '2026-11-13', scheduledTime: '08:30' })), undefined);
  assert.equal(previewFormError(form({ send: 'Gửi ngay' })), undefined);
  assert.equal(previewFormError(form({ name: 'Synthetic fixture' })), undefined);
});
