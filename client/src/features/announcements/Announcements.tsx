import { useState } from 'react';
import { DataTable, Facts, FilePicker, Pagination, Panel, PreviewForm, RouteLink, Search, Stats, Tabs, useListFilters } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';

const announcements = [
  { id: 'menu', title: 'Thực đơn tuần 02–06/11', audience: 'Toàn trường', time: '02/11 · 07:30', status: 'Đã gửi', read: '226/248 · 91,1%' },
  { id: 'nature', title: 'Bé khám phá thiên nhiên', audience: 'Toàn trường', time: '01/11 · 16:00', status: 'Đã gửi', read: '232/248 · 93,5%' },
  { id: 'care', title: 'Lưu ý chăm sóc bé khi giao mùa', audience: 'Toàn trường', time: '30/10 · 15:00', status: 'Đã gửi', read: '240/248 · 96,8%' },
  { id: 'health', title: 'Nhắc lịch khám sức khỏe định kỳ', audience: 'Toàn trường', time: '09/11 · 16:00', status: 'Đã lên lịch', read: 'Chưa gửi' },
  { id: 'thanks', title: 'Tri ân người gieo mầm', audience: 'Toàn trường', time: '16/11 · 08:00', status: 'Đã lên lịch', read: 'Chưa gửi' },
  { id: 'new-child', title: 'Chuẩn bị đồ dùng cho bé mới', audience: 'Mầm 2', time: '02/11 · 08:05', status: 'Bản nháp', read: 'Chưa gửi' },
  { id: 'books', title: 'Ngày hội đọc sách cùng bé', audience: 'Mầm, Chồi', time: '02/11 · 07:50', status: 'Bản nháp', read: 'Chưa gửi' },
  { id: 'pickup', title: 'Hướng dẫn đón trả trẻ an toàn', audience: 'Toàn trường', time: '29/10 · 15:30', status: 'Đã gửi', read: '248/248 · 100%' }
];
type Props = { id?: string; mode?: string; navigate: (url: string) => void };
export default function Announcements({ id, mode, navigate }: Props) {
  const [tab, setTab] = useState('Tất cả');
  const [audience, setAudience] = useState('');
  const [period, setPeriod] = useState('');
  const rows = announcements.filter(row => (tab === 'Tất cả' || row.status === tab) && (!audience || row.audience === audience) && (!period || row.time.includes(period)));
  const list = useListFilters(rows, row => row.title);
  const selected = announcements.find(row => row.id === id);
  if (mode === 'edit' && !selected) return <EmptyState title="Không tìm thấy thông báo mẫu"><RouteLink navigate={navigate} to="/preview/announcements">Về danh sách</RouteLink></EmptyState>;
  if (mode === 'new' || mode === 'edit') return <Compose title={selected?.title} navigate={navigate} />;
  if (id) return <><PageHeader title="Chi tiết thông báo" subtitle="Nội dung mẫu, không phải thông báo thật" /><Panel title={selected?.title ?? 'Không tìm thấy thông báo mẫu'}>{selected && <><Facts items={[[ 'Người nhận', selected.audience], ['Trạng thái thiết kế', selected.status], ['Thời gian mẫu', selected.time], ['Đã đọc mẫu', selected.read]]} /><Message>Nội dung chi tiết chưa được cung cấp trong ảnh. Không tự tạo nội dung đã gửi cho phụ huynh.</Message><RouteLink navigate={navigate} to={`/preview/announcements/${selected.id}/edit`}>Mở bản soạn xem trước</RouteLink></>}<RouteLink navigate={navigate} to="/preview/announcements">← Danh sách thông báo</RouteLink></Panel></>;
  return <>
    <PageHeader title="Thông báo" subtitle="Kết nối thông tin giữa nhà trường, giáo viên và phụ huynh." actions={<RouteLink className="button-link" to="/preview/announcements/new" navigate={navigate}>Soạn thông báo</RouteLink>} />
    <Stats items={[[ 'Đã gửi tháng này', '2', '01–02/11/2026 · mẫu'], ['Đã lên lịch', '2', 'Sắp gửi trong tháng 11 · mẫu'], ['Bản nháp', '2', 'Chưa gửi đến người nhận'], ['Lượt đọc tháng này', '92,3%', '458 / 496 lượt nhận · mẫu']]} />
    <Panel title="Danh sách thông báo"><Tabs items={['Tất cả', 'Đã gửi', 'Đã lên lịch', 'Bản nháp']} value={tab} onChange={value => { setTab(value); list.setPage(1); }} /><div className="filter-toolbar"><Search label="Tìm theo tiêu đề thông báo…" value={list.query} onChange={list.setQuery} /><label><span className="sr-only">Người nhận</span><select value={audience} onChange={e => { setAudience(e.target.value); list.setPage(1); }}><option value="">Tất cả người nhận</option>{['Toàn trường', 'Mầm 2', 'Mầm, Chồi'].map(item => <option key={item}>{item}</option>)}</select></label><label><span className="sr-only">Thời gian</span><select value={period} onChange={e => { setPeriod(e.target.value); list.setPage(1); }}><option value="">Tất cả thời gian</option><option value="/11">Tháng 11</option><option value="/10">Tháng 10</option></select></label></div>
      <DataTable label="Thông báo mẫu" rows={list.visible} rowKey={row => row.id} columns={[
        { label: 'Tiêu đề', render: row => row.title }, { label: 'Người nhận', render: row => row.audience }, { label: 'Thời gian', render: row => row.time },
        { label: 'Trạng thái', render: row => <span className={`badge ${row.status === 'Đã lên lịch' ? 'blue' : ''}`}>{row.status}</span> }, { label: 'Đã đọc', render: row => row.read },
        { label: 'Thao tác', render: row => <RouteLink navigate={navigate} to={`/preview/announcements/${row.id}${row.status === 'Bản nháp' ? '/edit' : ''}`}>{row.status === 'Bản nháp' ? 'Tiếp tục' : 'Chi tiết'}</RouteLink> }
      ]} /><Pagination count={list.filtered.length} page={list.page} onChange={list.setPage} />
    </Panel>
  </>;
}
function Compose({ title, navigate }: { title?: string; navigate: Props['navigate'] }) {
  const initialBody = 'Kính gửi Quý phụ huynh lớp Mầm 2,\n\nĐể bé có một tuần làm quen thật thoải mái, kính nhờ gia đình chuẩn bị:\n• 02 bộ quần áo dự phòng có ghi tên bé.\n• Bình nước cá nhân và khăn nhỏ.\n• Mũ khi tham gia hoạt động ngoài trời.\n\nVui lòng trao đổi với cô Phương Thảo nếu bé có lưu ý về sức khỏe. Cảm ơn Quý phụ huynh đã đồng hành cùng nhà trường!';
  const [values, setValues] = useState({ title: title ?? 'Chuẩn bị đồ dùng cho bé mới', body: initialBody, audience: 'Phụ huynh lớp Mầm 2', send: 'Gửi ngay' });
  return <><PageHeader title="Soạn thông báo" subtitle="Soạn nội dung và kiểm tra bản xem trước trước khi gửi · không gửi thật" /><PreviewForm groups={[
    { title: 'Nội dung thông báo', fields: [{ name: 'title', label: 'Tiêu đề', value: values.title, required: true, full: true }, { name: 'audience', label: 'Người nhận', options: ['Phụ huynh lớp Mầm 2', 'Toàn trường', 'Nhóm Mầm · Chồi'], required: true, full: true }, { name: 'body', label: 'Nội dung', multiline: true, value: initialBody, required: true, full: true }] },
    { title: 'Thời gian gửi', fields: [{ name: 'send', label: 'Hình thức', options: ['Gửi ngay', 'Lên lịch gửi'] }, { name: 'scheduledDate', label: 'Ngày gửi khi lên lịch', type: 'date' }, { name: 'scheduledTime', label: 'Giờ gửi khi lên lịch', type: 'time' }], note: 'Bản xem trước chỉ kiểm tra dữ liệu; không lập lịch hoặc lưu nháp thật.' }
  ]} onValues={data => setValues({ title: String(data.get('title') ?? ''), body: String(data.get('body') ?? ''), audience: String(data.get('audience') ?? ''), send: String(data.get('send') ?? '') })} submitLabel="Kiểm tra thông báo" cancel={<RouteLink className="button-link secondary" to="/preview/announcements" navigate={navigate}>Hủy</RouteLink>} aside={<><Panel title="Xem trước cho phụ huynh"><span className="badge">Bản nháp mẫu</span><p className="muted">Mầm non Hoa Ban · {values.audience}</p><h2>{values.title}</h2><p className="preserve-lines">{values.body}</p></Panel><Panel title="Kiểm tra trước khi gửi"><Facts items={[[ 'Người gửi', 'Nguyễn Mai Lan · mẫu'], ['Người nhận', values.audience], ['Hình thức', 'Thông báo ứng dụng · mẫu'], ['Thời gian', values.send]]} /><Message>Chưa gửi đến bất kỳ người nhận nào. Không có API phát hành.</Message></Panel></>}><Panel title="Tệp đính kèm"><FilePicker label="Thêm tệp đính kèm" /></Panel></PreviewForm></>;
}
