import assert from 'node:assert/strict';
import { test } from 'node:test';
import { previewYears, yearError, yearPhase } from '../src/features/school/year-rules.ts';

test('school year requires name and strictly increasing dates', () => {
  assert.match(yearError({ name: ' ', startDate: '2028-09-01', endDate: '2029-05-31' }, []), /Tên/);
  assert.match(yearError({ name: 'x'.repeat(101), startDate: '2028-09-01', endDate: '2029-05-31' }, []), /Tên/);
  for (const endDate of ['', '2028-09-01', '2028-08-31'])
    assert.match(yearError({ name: 'Test', startDate: '2028-09-01', endDate }, []), /Ngày kết thúc/);
  assert.equal(yearError({ name: 'Next', startDate: '2028-09-01', endDate: '2029-05-31' }, previewYears), undefined);
});

test('name normalization and inclusive overlap apply within the loaded school years', () => {
  assert.match(yearError({ name: ' 2026–2027 ', startDate: '2030-09-01', endDate: '2031-05-31' }, previewYears), /tồn tại/);
  const years = [{ ...previewYears[0], name: 'Năm Học Mới' }];
  assert.match(yearError({ name: 'năm học mới', startDate: '2030-09-01', endDate: '2031-05-31' }, years), /tồn tại/);
  for (const startDate of ['2026-10-01', '2027-05-31'])
    assert.match(yearError({ name: 'Other', startDate, endDate: '2027-08-31' }, previewYears), /Khoảng ngày/);
  assert.equal(yearError({ name: 'Summer', startDate: '2027-06-01', endDate: '2027-08-31' }, previewYears), undefined);
});

test('editing excludes itself, locks dates for used year, allows unused date edits', () => {
  const used = previewYears[1];
  assert.equal(yearError({ ...used, name: 'Renamed' }, previewYears, used), undefined);
  assert.match(yearError({ ...used, startDate: '2026-09-02' }, previewYears, used), /chỉ được sửa tên/);
  const unused = previewYears[2];
  assert.equal(yearError({ ...unused, endDate: '2028-06-30' }, previewYears, unused), undefined);
});

test('phase is derived from dates, inclusive at both boundaries', () => {
  const year = previewYears[1];
  assert.equal(yearPhase(year, '2026-08-31'), 'Sắp bắt đầu');
  assert.equal(yearPhase(year, '2026-09-01'), 'Trong kỳ');
  assert.equal(yearPhase(year, '2027-05-31'), 'Trong kỳ');
  assert.equal(yearPhase(year, '2027-06-01'), 'Đã kết thúc');
});
