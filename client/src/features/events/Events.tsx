import { useState } from 'react';
import { DataTable, Facts, Panel, PreviewForm, RouteLink, Tabs } from '../../components/design';
import { EmptyState, Message, PageHeader } from '../../components/ui';

export const events = [
  { id: 'nature', day: '2026-11-05', time: '08:30', end: '10:00', title: 'Bé khám phá thiên nhiên', place: 'Vườn trường', category: 'Ngoại khóa', tone: 'green', description: 'Các bé cùng quan sát cây xanh, nhận biết lá và hoa trong Vườn trường. Hoạt động giúp trẻ phát triển khả năng quan sát, tình yêu thiên nhiên và thói quen chăm sóc môi trường.' },
  { id: 'health', day: '2026-11-10', time: '08:00', end: '11:00', title: 'Khám sức khỏe định kỳ', place: 'Phòng y tế', category: 'Sức khỏe', tone: 'blue', description: 'Lịch kiểm tra sức khỏe định kỳ minh họa theo kế hoạch trường.' },
  { id: 'gratitude', day: '2026-11-20', time: '08:30', end: '10:30', title: 'Tri ân người gieo mầm', place: 'Hội trường', category: 'Lễ hội', tone: 'amber', description: 'Hoạt động tri ân giáo viên trong tháng 11.' }
];
type Props = { id?: string; mode?: string; navigate: (url: string) => void };
export default function Events({ id, mode, navigate }: Props) {
  const [month, setMonth] = useState(new Date(2026, 10, 1));
  const [tab, setTab] = useState('Tháng');
  const event = events.find(item => item.id === id);
  if (mode === 'edit' && !event) return <EmptyState title="Không tìm thấy sự kiện mẫu"><RouteLink navigate={navigate} to="/preview/events">Về lịch</RouteLink></EmptyState>;
  if (mode === 'new' || mode === 'edit') return <EventForm navigate={navigate} event={mode === 'edit' ? event : undefined} />;
  if (id) return event ? <EventDetail event={event} navigate={navigate} /> : <EmptyState title="Không tìm thấy sự kiện mẫu"><RouteLink navigate={navigate} to="/preview/events">Về lịch</RouteLink></EmptyState>;
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const start = new Date(first); start.setDate(1 - (first.getDay() + 6) % 7);
  const monthEvents = events.filter(item => new Date(`${item.day}T12:00:00`).getMonth() === month.getMonth() && new Date(`${item.day}T12:00:00`).getFullYear() === month.getFullYear());
  const move = (offset: number) => setMonth(new Date(month.getFullYear(), month.getMonth() + offset, 1));
  return <>
    <PageHeader title="Lịch & sự kiện" subtitle="Những hoạt động đáng nhớ cùng các bé · dữ liệu mẫu tháng 11/2026" actions={<RouteLink className="button-link" to="/preview/events/new" navigate={navigate}>+ Tạo sự kiện</RouteLink>} />
    <div className="split-layout"><Panel title={`Tháng ${month.getMonth() + 1}, ${month.getFullYear()}`}><div className="calendar-toolbar"><button className="secondary" aria-label="Tháng trước" onClick={() => move(-1)}>‹</button><span>Tháng {month.getMonth() + 1}, {month.getFullYear()}</span><button className="secondary" aria-label="Tháng sau" onClick={() => move(1)}>›</button><button className="secondary" onClick={() => { const today = new Date(); setMonth(new Date(today.getFullYear(), today.getMonth(), 1)); }}>Hôm nay</button></div><Tabs items={['Tháng', 'Tuần', 'Danh sách']} value={tab} onChange={setTab} />
      {tab === 'Tháng' ? <div className="calendar-scroll" tabIndex={0} role="region" aria-label="Lịch tháng mẫu"><div className="calendar-grid">{['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'].map(day => <div className="calendar-day-name" key={day}>{day}</div>)}{Array.from({ length: 42 }, (_, i) => {
        const day = new Date(start); day.setDate(start.getDate() + i);
        const dateKey = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
        return <div key={dateKey} className={`calendar-cell ${day.getMonth() !== month.getMonth() ? 'outside' : ''}`}><time dateTime={dateKey}>{day.getDate()}</time>{events.filter(item => item.day === dateKey).map(item => <RouteLink key={item.id} className={`calendar-event ${item.tone}`} navigate={navigate} to={`/preview/events/${item.id}`}>{item.time}<br />{item.title}</RouteLink>)}</div>;
      })}</div></div> : <div>{tab === 'Tuần' && <p className="muted">Tuần bắt đầu ngày 02/11 trong tháng mẫu; tháng khác không có sự kiện mẫu tuần đầu.</p>}{monthEvents.filter(item => tab !== 'Tuần' || Number(item.day.slice(-2)) <= 8).map(item => <EventSummary key={item.id} item={item} navigate={navigate} />)}{!monthEvents.length && <EmptyState title="Không có sự kiện mẫu trong tháng" />}</div>}
    </Panel><aside className="stack"><Panel title="Sự kiện sắp tới"><p className="muted">{monthEvents.length} sự kiện mẫu trong tháng</p>{monthEvents.map(item => <EventSummary key={item.id} item={item} navigate={navigate} />)}</Panel><Panel title="Nhóm tham gia"><p>Hoạt động ngoại khóa: toàn bộ 12 lớp, chia nhóm theo độ tuổi.</p><p className="muted">Thông tin minh họa. Không gửi thông báo thật.</p></Panel></aside></div>
  </>;
}
function EventSummary({ item, navigate }: { item: typeof events[number]; navigate: Props['navigate'] }) {
  return <article className="event-summary"><div className="section-title"><strong className="large-green">{item.day.slice(-2)}/11</strong><span className={`badge ${item.tone}`}>{item.category}</span></div><h3>{item.title}</h3><p className="muted">{item.time}–{item.end} · {item.place}</p><RouteLink navigate={navigate} to={`/preview/events/${item.id}`}>Xem chi tiết →</RouteLink></article>;
}
function EventDetail({ event, navigate }: { event: typeof events[number]; navigate: Props['navigate'] }) {
  const [tab, setTab] = useState('Thông tin sự kiện');
  if (event.id !== 'nature') return <>
    <PageHeader title="Chi tiết sự kiện" subtitle={event.title} actions={<RouteLink className="button-link" navigate={navigate} to={`/preview/events/${event.id}/edit`}>Chỉnh sửa sự kiện</RouteLink>} />
    <Panel title={event.title}><p>{event.description}</p><Facts items={[[ 'Ngày', event.day], ['Thời gian', `${event.time}–${event.end}`], ['Địa điểm', event.place], ['Danh mục', event.category]]} /><Message>Chỉ có dữ liệu tóm tắt mẫu. Chưa có chương trình, người điều phối hoặc danh sách tham gia cho sự kiện này.</Message></Panel>
    <RouteLink navigate={navigate} to="/preview/events">← Về lịch & sự kiện</RouteLink>
  </>;
  return <>
    <PageHeader title="Chi tiết sự kiện" subtitle={`Lịch & sự kiện / ${event.title}`} actions={<><button className="secondary" onClick={() => window.print()}>In danh sách mẫu</button><RouteLink className="button-link" navigate={navigate} to={`/preview/events/${event.id}/edit`}>Chỉnh sửa sự kiện</RouteLink></>} />
    <section className="card profile-heading"><div className="section-title"><h1>{event.title}</h1><span className="badge">Sắp diễn ra · mẫu</span></div><p className="muted">{event.day} · {event.time}–{event.end} · {event.place}</p><Tabs items={['Thông tin sự kiện', 'Danh sách tham gia', 'Tài liệu']} value={tab} onChange={setTab} /></section>
    {tab === 'Thông tin sự kiện' ? <div className="split-layout"><div className="stack"><Panel title="Về hoạt động"><p>{event.description}</p><Facts items={[[ 'Danh mục', event.category], ['Đối tượng', 'Toàn trường · 12 lớp · 248 học sinh · mẫu'], ['Hình thức', 'Chia nhóm theo độ tuổi, giáo viên đi cùng lớp']]} /></Panel><Panel title="Chương trình hoạt động"><DataTable label="Chương trình mẫu" rows={['Tập trung, chia nhóm và hướng dẫn an toàn', 'Quan sát cây, lá và hoa', 'Bé tập trồng và tưới cây', 'Chia sẻ khám phá, vệ sinh và về lớp'].map((title, i) => ({ title, i }))} rowKey={row => String(row.i)} columns={[{ label: 'Thời gian', render: row => ['08:30–08:40', '08:40–09:15', '09:15–09:45', '09:45–10:00'][row.i] }, { label: 'Hoạt động', render: row => row.title }, { label: 'Phụ trách', render: () => 'Giáo viên các lớp · mẫu' }]} /></Panel><Message>Lời nhắn mẫu đến phụ huynh: trang phục thoải mái, mũ và bình nước có ghi tên. Không có thông báo nào được gửi.</Message></div><aside className="stack"><Panel title="Người điều phối"><h2>Nguyễn Thu Hà</h2><Facts items={[[ 'Liên hệ mẫu', '0900 100 001'], ['Hỗ trợ', 'Đỗ Phương Thảo'], ['Địa điểm', event.place], ['Chuẩn bị', 'Dụng cụ trồng cây, nước rửa tay']]} /></Panel><Panel title="Thông tin tham gia"><Facts items={[[ 'Nhà trẻ', '32 học sinh · 2 lớp'], ['Mầm', '66 học sinh · 3 lớp'], ['Chồi', '70 học sinh · 3 lớp'], ['Lá', '80 học sinh · 4 lớp'], ['Giáo viên đi cùng', '24 giáo viên · mẫu']]} /></Panel></aside></div> : <Panel title={tab}><EmptyState title={tab === 'Tài liệu' ? 'Chưa có tài liệu mẫu' : 'Danh sách lớp minh họa'}>{tab !== 'Tài liệu' && 'Nhà trẻ 1–2, Mầm 1–3, Chồi 1–3, Lá 1–4. Chưa có API danh sách tham gia.'}</EmptyState></Panel>}
    <RouteLink navigate={navigate} to="/preview/events">← Về lịch & sự kiện</RouteLink>
  </>;
}
function EventForm({ event, navigate }: { event?: typeof events[number]; navigate: Props['navigate'] }) {
  return <><PageHeader title={event ? 'Chỉnh sửa sự kiện' : 'Tạo sự kiện'} subtitle="Lên kế hoạch hoạt động và chia sẻ cùng giáo viên, phụ huynh · bản mẫu" /><PreviewForm groups={[
    { title: 'Thông tin sự kiện', fields: [{ name: 'title', label: 'Tên sự kiện', value: event?.title ?? 'Ngày hội đọc sách cùng bé', required: true, full: true }, { name: 'category', label: 'Danh mục', options: ['Hoạt động học tập', 'Ngoại khóa', 'Sức khỏe', 'Lễ hội'], value: event?.category, required: true }, { name: 'place', label: 'Địa điểm', value: event?.place ?? 'Thư viện trường', required: true }, { name: 'description', label: 'Mô tả', value: event?.description ?? 'Các bé lựa chọn sách yêu thích, nghe cô kể chuyện và cùng làm dấu trang.', multiline: true, full: true, required: true }] },
    { title: 'Thời gian tổ chức', fields: [{ name: 'startDate', label: 'Ngày bắt đầu', type: 'date', value: event?.day ?? '2026-11-13', required: true }, { name: 'endDate', label: 'Ngày kết thúc', type: 'date', value: event?.day ?? '2026-11-13', required: true }, { name: 'startTime', label: 'Giờ bắt đầu', type: 'time', value: event?.time ?? '08:30', required: true }, { name: 'endTime', label: 'Giờ kết thúc', type: 'time', value: event?.end ?? '10:00', required: true }] },
    { title: 'Lưu ý cho phụ huynh', fields: [{ name: 'note', label: 'Lời nhắn', value: 'Bé có thể mang theo một quyển sách yêu thích, ghi tên và lớp trên bìa.', multiline: true, full: true }] }
  ]} submitLabel="Kiểm tra sự kiện" cancel={<RouteLink className="button-link secondary" navigate={navigate} to="/preview/events">Hủy</RouteLink>} aside={<><Panel title="Đối tượng & điều phối"><label>Lớp tham gia<select name="audience"><option>Nhóm Mầm · Nhóm Chồi</option><option>Toàn trường</option></select></label><label>Người điều phối<select name="coordinator"><option>Đỗ Phương Thảo</option><option>Nguyễn Thu Hà</option></select></label><label>Thông báo nhắc trước<select name="reminder"><option>1 ngày</option><option>Không nhắc</option></select></label></Panel><Panel title="Kiểm tra kế hoạch"><Facts items={[[ 'Tham gia', '136 học sinh · 6 lớp · mẫu'], ['Giáo viên', '12 giáo viên · mẫu']]} /></Panel><Message>Chưa gửi thông báo. Việc kiểm tra biểu mẫu không lưu lịch hay gửi đến phụ huynh.</Message></>} /></>;
}
