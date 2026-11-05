import assert from 'node:assert/strict';
import { test } from 'node:test';
import { activeAssignments, teacherStatus, teacherError, teacherForm } from '../src/features/people/teacher-rules.ts';

const profile = {
  name: 'Synthetic Teacher', birthday: '1995-04-20', gender: 'Nữ', phone: '0900000000',
  email: 'teacher@synthetic.test', address: '', degree: 'Đại học Sư phạm mầm non', graduated: 2017,
  university: 'Synthetic University', experience: 5, certificate: '', startDate: '2022-08-01',
  contract: 'Không xác định thời hạn', work: 'Toàn thời gian', isActive: true, userId: null
};
test('teacher profile validation accepts valid data and rejects invalid required fields', () => {
  assert.equal(teacherError(profile, '2026-11-04'), undefined);
  for (const patch of [
    { name: '' }, { name: 'x'.repeat(201) }, { birthday: '2099-01-01' }, { startDate: '1990-01-01' },
    { phone: '123' }, { email: 'invalid' }, { experience: -1 }, { experience: 1.5 },
    { graduated: 2100 }, { university: '' }, { degree: 'unknown' }, { address: 'x'.repeat(501) }
  ]) assert.ok(teacherError({ ...profile, ...patch }, '2026-11-04'));
});
test('form conversion trims fields and links only explicitly selected user', () => {
  const data = new FormData();
  for (const [key, value] of Object.entries(profile)) if (value !== null) data.set(key, String(value));
  data.set('status', 'Đang công tác'); data.set('name', ' Synthetic Teacher ');
  assert.deepEqual(teacherForm(data), profile);
  data.set('userId', 'synthetic-user-id'); data.set('status', 'Tạm nghỉ');
  assert.equal(teacherForm(data).userId, 'synthetic-user-id');
  assert.equal(teacherForm(data).isActive, false);
});
test('only unended assignments appear as active and employee status is independent', () => {
  const teacher = { id: 'synthetic-id', code: 'GV-synthetic', profile, assignments: [
    { id: 'past', endedAtUtc: '2026-11-04T00:00:00Z' }, { id: 'current', endedAtUtc: null }
  ] };
  assert.deepEqual(activeAssignments(teacher).map(row => row.id), ['current']);
  assert.equal(teacherStatus(teacher), 'Đang công tác');
  assert.equal(teacherStatus({ ...teacher, profile: { ...profile, isActive: false } }), 'Tạm nghỉ');
});
