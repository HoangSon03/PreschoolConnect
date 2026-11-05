import test from 'node:test';
import assert from 'node:assert/strict';
import { currentEnrollment, enrollmentForm, studentError, studentForm } from '../src/features/people/student-rules.ts';

function form() {
  const data = new FormData();
  for (const [key, value] of Object.entries({ name: 'Synthetic Child', birthday: '2022-01-01', gender: 'Nam', address: 'Synthetic address',
    guardian: 'Synthetic Guardian', relationship: 'Bố', phone: '0900000000', email: '', pickup: '', emergency: '0900000001',
    height: '97,5', weight: '14,8', care: '', classroomId: 'class-id', startDate: '2026-11-04', ageGroup: 'Mầm · 3–4 tuổi', studyMode: 'Bán trú' })) data.set(key, value);
  return data;
}
test('Student form maps real fields and decimal comma without inventing account or ownership', () => {
  const p = studentForm(form());
  assert.equal(p.weight, 14.8);
  assert.equal(p.height, 97.5);
  assert.equal(studentError(p, '2026-11-04'), '');
  assert.equal('schoolId' in p, false);
  assert.equal('userId' in p, false);
  assert.equal('code' in p, false);
  assert.equal(enrollmentForm(form()).classroomId, 'class-id');
});
test('Optional measurements stay null; invalid numbers, lengths and phones are rejected', () => {
  const data = form(); data.set('height', ''); data.set('weight', '');
  const p = studentForm(data);
  assert.equal(p.height, null); assert.equal(p.weight, null);
  for (const change of [{ weight: NaN }, { height: 251 }, { weight: 14.888 }, { phone: '123' }, { birthday: '2099-01-01' },
    { care: 'x'.repeat(2001) }, { name: '' }, { relationship: 'unknown' }])
    assert.notEqual(studentError({ ...p, ...change }, '2026-11-04'), '');
});
test('Current enrollment uses exclusive end and server school-local date', () => {
  const student = { today: '2026-11-04', enrollments: [
    { id: 'old', startDate: '2026-09-01', endDate: '2026-11-04' },
    { id: 'new', startDate: '2026-11-04', endDate: '2027-07-01' }
  ] };
  assert.equal(currentEnrollment(student)?.id, 'new');
  assert.equal(currentEnrollment({ ...student, today: '2027-07-01' }), undefined);
});
