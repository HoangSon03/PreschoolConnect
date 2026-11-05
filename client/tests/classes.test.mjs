import test from 'node:test';
import assert from 'node:assert/strict';
import { classTotals, classroomError, monday } from '../src/features/classes/classroom-rules.ts';

test('Class totals use actual roster and distinct assigned teachers, not summed assignments', () => {
  const rows = [
    { studentCount: 2, capacity: 1, teachers: [{ id: 'a' }, { id: 'b' }] },
    { studentCount: 3, capacity: 10, teachers: [{ id: 'a' }] }
  ];
  assert.deepEqual(classTotals(rows), { students: 5, teachers: 2, capacity: 11 });
  assert.equal(classTotals([...rows, { studentCount: 0, capacity: null, teachers: [] }]).capacity, null);
});
test('Schedule permits adjacent intervals, rejects overlap, invalid hours, empty activities and excessive rows', () => {
  const rows = [{ start: '07:00', end: '08:00', activity: 'Synthetic activity' },
    { start: '08:00', end: '09:00', activity: 'Synthetic activity' }];
  const valid = schedule => classroomError('Synthetic Class', '', '', schedule, []);
  assert.equal(valid(rows), '');
  for (const change of [{ start: '07:59' }, { end: '07:00' }, { start: '25:00' }, { activity: ' ' }, { activity: 'x'.repeat(201) }])
    assert.notEqual(valid([rows[0], { ...rows[1], ...change }]), '');
  assert.notEqual(valid(Array(21).fill(rows[0])), '');
});
test('Capacity is optional informational metadata; week notes must be unique Mondays and valid dates', () => {
  assert.equal(classroomError('Synthetic', 'Room', '500', [], []), '');
  assert.equal(classroomError('Synthetic', '', '', [], []), '');
  for (const capacity of ['0', '501', '2.5', 'abc'])
    assert.notEqual(classroomError('Synthetic', '', capacity, [], []), '');
  const note = { weekStart: '2026-11-02', text: 'Synthetic note' };
  assert.equal(classroomError('Synthetic', '', '1', [], [note]), '');
  for (const notes of [[note, note], [{ ...note, weekStart: '2026-11-03' }], [{ ...note, weekStart: '2026-02-30' }],
    [{ ...note, weekStart: '' }], [{ ...note, text: '' }], [{ ...note, text: 'x'.repeat(2001) }], Array(53).fill(note)])
    assert.notEqual(classroomError('Synthetic', '', '', [], notes), '');
  assert.equal(monday('2026-11-08'), '2026-11-02');
  assert.equal(monday('2026-11-02'), '2026-11-02');
});
