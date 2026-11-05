import { useState, type FormEvent } from 'react';
import { DataTable, Facts, Pagination, Panel, RouteLink, Search, Stats, Tabs, useListFilters } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';
import { feeFixtures as fixtures, money } from './data';
export default function Fees({ id, navigate }: { id?: string; navigate: (url: string) => void }) {
  const [tab, setTab] = useState('Tất cả');
  const [classroom, setClassroom] = useState('');
  const [month, setMonth] = useState('2026-11');
  const list = useListFilters(fixtures.filter(row => (tab === 'Tất cả' || row.status === tab) && (!classroom || row.classroom === classroom) && month === '2026-11'), row => `${row.name} ${row.id}`);
  const selected = fixtures.find(row => row.id === id);
  if (id) return selected ? <FeeDetail row={selected} navigate={navigate} /> : <EmptyState title="Không tìm thấy phiếu mẫu"><RouteLink navigate={navigate} to="/preview/fees">Về danh sách</RouteLink></EmptyState>;
  return <>
    <PageHeader title="Học phí" subtitle="Theo dõi khoản phải thu và tình hình thanh toán tháng 11/2026 · mẫu" actions={<><button className="secondary" onClick={() => window.print()}>In công nợ mẫu</button><button disabled title="Chưa có thiết kế tạo phiếu thu và API thu phí">+ Tạo phiếu thu</button></>} />
    <Stats items={[[ 'Tổng phải thu', '855,6 tr', '248 học sinh · Tháng 11/2026 · mẫu'], ['Đã thu', '630 tr', '73,6% tổng phải thu · mẫu'], ['Còn phải thu', '225,6 tr', '68 hồ sơ còn dư nợ · mẫu'], ['Đã thanh toán đủ', '180 / 248', '8 một phần · 60 chưa thanh toán · mẫu']]} />
    <Message>UI-only: học phí/thanh toán nằm ngoài MVP. Không kết nối ngân hàng, không thu tiền hoặc cập nhật công nợ thật.</Message>
    <Panel title="Danh sách học phí học sinh"><Tabs items={['Tất cả', 'Đã thanh toán', 'Một phần', 'Chưa thanh toán']} value={tab} onChange={value => { setTab(value); list.setPage(1); }} /><div className="filter-toolbar"><Search value={list.query} onChange={list.setQuery} label="Tìm tên học sinh hoặc mã phiếu…" /><label>Kỳ thu<input type="month" value={month} onChange={e => { setMonth(e.target.value); list.setPage(1); }} /></label><label>Lớp<select value={classroom} onChange={e => { setClassroom(e.target.value); list.setPage(1); }}><option value="">Tất cả lớp</option>{['Mầm 2', 'Chồi 1', 'Lá 1'].map(item => <option key={item}>{item}</option>)}</select></label></div>
      <DataTable label="Học phí mẫu" rows={list.visible} rowKey={row => row.id} columns={[
        { label: 'Mã phiếu', render: row => `HP11-${row.id.slice(2)}` }, { label: 'Học sinh / Lớp', render: row => <>{row.name}<small className="block muted">{row.classroom}</small></> },
        { label: 'Phải thu', render: row => money(row.due) }, { label: 'Đã thu', render: row => money(row.paid) }, { label: 'Còn lại', render: row => money(row.due - row.paid) },
        { label: 'Trạng thái', render: row => <span className={`badge ${row.status === 'Một phần' ? 'amber' : ''}`}>{row.status}</span> }, { label: 'Thao tác', render: row => <RouteLink navigate={navigate} to={`/preview/fees/${row.id}`}>Chi tiết</RouteLink> }
      ]} /><Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} />
    </Panel>
  </>;
}
function FeeDetail({ row, navigate }: { row: typeof fixtures[number]; navigate: (url: string) => void }) {
  const [amount, setAmount] = useState(String(row.due - row.paid));
  const [notice, setNotice] = useState('');
  function check(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(`Chỉ xem trước: nếu ghi nhận ${money(Number(amount))}, số dư sẽ là ${money(row.due - row.paid - Number(amount))}. Chưa ghi nhận thanh toán hay tạo biên nhận.`);
  }
  return <>
    <PageHeader title="Chi tiết học phí" subtitle={`Phiếu HP11-${row.id.slice(2)} · Kỳ thu tháng 11/2026 · Hạn thanh toán 10/11/2026`} actions={<button className="secondary" onClick={() => window.print()}>In phiếu mẫu</button>} />
    <section className="card person-heading"><span className="initials">{row.name.split(' ').slice(-2).map(s => s[0]).join('')}</span><div><h2>{row.name}</h2><p className="muted">{row.id} · {row.classroom} · Phụ huynh mẫu: {row.contact}</p></div><span className="badge amber">{row.status}</span></section>
    <div className="split-layout"><div className="stack"><Panel title="Các khoản thu tháng 11"><DataTable label="Khoản thu mẫu" rows={[['Học phí bán trú', '1 tháng', 2400000, 2400000], ['Tiền ăn (21 ngày học)', '21 ngày', 50000, 1050000], ['Học phẩm & vệ sinh', '1 tháng', 200000, 200000]]} rowKey={r => String(r[0])} columns={[{ label: 'Khoản thu', render: r => r[0] }, { label: 'Số lượng', render: r => r[1] }, { label: 'Đơn giá', render: r => money(Number(r[2])) }, { label: 'Thành tiền', render: r => money(Number(r[3])) }]} /><Facts items={[[ 'Cộng các khoản thu', money(3650000)], ['Ưu đãi nhập học', money(-200000)], ['Tổng phải thanh toán', money(row.due)]]} /><Message>Đã đối chiếu số liệu mẫu: 3.650.000 − 200.000 = 3.450.000 đ. Không phải hóa đơn thật.</Message></Panel><Panel title="Lịch sử thanh toán">{row.paid ? <DataTable label="Thanh toán mẫu" rows={[row]} rowKey={r => r.id} columns={[{ label: 'Ngày', render: () => '02/11/2026' }, { label: 'Mã biên nhận', render: () => 'PT-11001 · mẫu' }, { label: 'Hình thức', render: () => 'Chuyển khoản' }, { label: 'Số tiền', render: r => money(r.paid) }]} /> : <EmptyState title="Chưa có thanh toán mẫu" />}</Panel><Panel title="Thông tin thanh toán"><Facts items={[[ 'Người thanh toán', `${row.contact} · mẫu`], ['Hạn thanh toán', '10/11/2026'], ['Nội dung chuyển khoản', `HP11-${row.id} ${row.name}`]]} /><Message>Không sử dụng thông tin minh họa để chuyển tiền.</Message></Panel></div><aside className="stack"><Panel title="Tổng kết phiếu thu"><Facts items={[[ 'Tổng phải thu', money(row.due)], ['Đã thanh toán', money(row.paid)]]} /><div className="amount-summary"><small>Còn phải thu</small><h1>{money(row.due - row.paid)}</h1></div></Panel><Panel title="Ghi nhận thanh toán"><form onSubmit={check}><label>Số tiền nhận thêm *<input type="number" min="1" max={row.due - row.paid} required value={amount} onChange={e => { setAmount(e.target.value); setNotice(''); }} /></label><label>Ngày thanh toán *<input type="date" required defaultValue="2026-11-02" /></label><label>Hình thức<select><option>Chuyển khoản</option><option>Tiền mặt</option></select></label><label>Mã giao dịch / Ghi chú<input placeholder="Nhập mã tham chiếu…" /></label><button disabled={row.due === row.paid}>Kiểm tra thanh toán mẫu</button></form>{notice && <Message>{notice}</Message>}<p className="muted">Chưa lưu khoản thu. Không tạo biên nhận thật.</p></Panel></aside></div>
    <RouteLink navigate={navigate} to="/preview/fees">← Danh sách học phí</RouteLink>
  </>;
}
