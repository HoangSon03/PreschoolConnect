import { useState } from 'react';
import { DataTable, Panel, Stats, Tabs } from '../../components/design';
import { EmptyState, PageHeader } from '../../components/ui';
import { previewGroups } from '../classes/preview-data';

export default function Reports() {
  const [tab, setTab] = useState('Tổng hợp');
  const [group, setGroup] = useState('');
  const [start, setStart] = useState('2026-11-01');
  const [end, setEnd] = useState('2026-11-02');
  const [period, setPeriod] = useState('2026-11');
  const available = start <= '2026-11-02' && end >= '2026-11-02' && start <= end && period === '2026-11';
  const rows = previewGroups.filter(row => !group || row.name === group);
  return <>
    <PageHeader title="Báo cáo" subtitle="Tổng hợp chuyên cần, tuyển sinh và học phí theo nhóm lớp · dữ liệu mẫu" actions={<button className="secondary" onClick={() => window.print()}>In báo cáo mẫu</button>} />
    <Panel title="Bộ lọc báo cáo"><div className="four-columns"><label>Từ ngày<input type="date" value={start} onChange={e => setStart(e.target.value)} /></label><label>Đến ngày<input type="date" min={start} value={end} onChange={e => setEnd(e.target.value)} /></label><label>Nhóm lớp<select value={group} onChange={e => setGroup(e.target.value)}><option value="">Tất cả nhóm</option>{previewGroups.map(row => <option key={row.name}>{row.name}</option>)}</select></label><label>Kỳ học phí<input type="month" value={period} onChange={e => setPeriod(e.target.value)} /></label></div><p className="muted">Chỉ có bản chụp mẫu ngày 02/11/2026, kỳ thu 11/2026. Bộ lọc không truy vấn API.</p></Panel>
    {!available ? <Panel title="Kết quả"><EmptyState title="Không có dữ liệu mẫu trong khoảng đã chọn" /></Panel> : <>
      <Stats items={[[ 'Học sinh đang học', String(rows.reduce((sum, row) => sum + row.total, 0)), 'Theo bộ lọc nhóm · số liệu mẫu'], ['Chuyên cần 02/11', '95,2%', 'Tỷ lệ toàn trường mẫu, không tính lại theo nhóm'], ['Đã thu tháng 11', '630 tr', 'Toàn trường · số liệu mẫu'], ['Tỷ lệ thu học phí', '73,6%', 'Toàn trường · số liệu mẫu']]} />
      <Tabs items={['Tổng hợp', 'Chuyên cần', 'Tuyển sinh', 'Học phí']} value={tab} onChange={setTab} />
      {(tab === 'Tổng hợp' || tab === 'Chuyên cần') && <div className="equal-columns"><Panel title="Chuyên cần 5 ngày học gần nhất"><p className="muted">Tỷ lệ có mặt toàn trường · mẫu</p><div className="report-bars">{[93.5, 94.8, 96, 93.1, 95.2].map((value, i) => <div key={i}><small>{value.toLocaleString('vi-VN')}%</small><span style={{ height: `${value * 1.5}px` }} /><small>{['27/10', '28/10', '29/10', '30/10', '02/11'][i]}</small></div>)}</div></Panel><Panel title="Phân bố học sinh theo nhóm tuổi">{rows.map(row => <div className="distribution" key={row.name}><span>{row.name}</span><progress max={80} value={row.total} aria-label={`Số học sinh ${row.name}`} /><span>{row.total}</span></div>)}</Panel></div>}
      <Panel title={tab === 'Tuyển sinh' ? 'Tuyển sinh theo nhóm lớp' : tab === 'Học phí' ? 'Học phí theo nhóm lớp' : 'Tổng hợp theo nhóm lớp'}><DataTable label="Báo cáo mẫu theo nhóm lớp" rowKey={row => row.name} rows={rows} columns={[
        { label: 'Nhóm lớp', render: row => row.name }, { label: 'Số lớp', render: row => row.sizes.length }, { label: 'Học sinh', render: row => row.total },
        { label: 'Có mặt', render: row => ({ 'Nhà trẻ': 30, 'Mầm': 63, 'Chồi': 67, 'Lá': 76 })[row.name] }, { label: 'Phải thu (tr)', render: row => (row.total * 3.45).toLocaleString('vi-VN') },
        { label: 'Đã thu (tr)', render: row => ({ 'Nhà trẻ': 80, 'Mầm': 170, 'Chồi': 180, 'Lá': 200 })[row.name] }
      ]} />{tab === 'Tuyển sinh' && <p className="muted">Ảnh chỉ có tổng học sinh theo nhóm. Chưa có dữ liệu mẫu xu hướng tuyển sinh, không tự suy diễn.</p>}</Panel>
    </>}
  </>;
}
