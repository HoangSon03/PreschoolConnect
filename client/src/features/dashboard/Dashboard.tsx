import { EmptyState, Metric, PageHeader } from '../../components/ui';
import { previewGroups } from '../classes/preview-data';
import { RouteLink } from '../../components/design';

export default function Dashboard({ preview, classCount, fullName, navigate }: { preview: boolean; classCount: number; fullName: string; navigate: (url: string) => void }) {
  return <>
    <PageHeader title={`Chào buổi sáng, ${fullName}!`} subtitle="Một ngày mới, cùng chăm sóc những mầm xanh." actions={<>{preview ? <><RouteLink className="button-link secondary" to="/preview/reports" navigate={navigate}>Xem báo cáo mẫu</RouteLink><RouteLink className="button-link" to="/preview/students/new" navigate={navigate}>+ Thêm học sinh</RouteLink></> : <><button className="secondary" disabled title="Chưa có API xuất báo cáo">Xuất báo cáo</button><button disabled title="Chưa có API học sinh">+ Thêm học sinh</button></>}</>} />
    <div className="metrics">
      <Metric label="Tổng số học sinh" value={preview ? '248' : '—'} unit="học sinh" note={preview ? '+8 học sinh · so với tháng trước' : 'Chưa có API thống kê học sinh'} />
      <Metric label="Lớp học" value={String(classCount)} unit="lớp" note={preview ? 'Đang hoạt động · 4 nhóm độ tuổi' : 'Số lớp từ hệ thống'} tone="amber" />
      <Metric label="Đội ngũ giáo viên" value={preview ? '24' : '—'} unit="giáo viên" note={preview ? 'Đủ nhân sự · 2 giáo viên / lớp' : 'Chưa có API giáo viên'} tone="purple" />
      <Metric label="Chuyên cần hôm nay" value={preview ? '95,2' : '—'} unit="%" note={preview ? '+2,1 điểm % · 236 / 248 học sinh có mặt' : 'Chưa có API điểm danh'} tone="blue" />
    </div>
    {!preview ? <section className="card"><EmptyState title="Chưa có dữ liệu tổng quan">Các biểu đồ điểm danh, sự kiện và hoạt động sẽ hiển thị khi có API tương ứng. Không dùng dữ liệu mẫu trong không gian thật.</EmptyState></section> :
      <div className="dashboard-grid">
        <section className="card chart-card"><h2>Tình hình chuyên cần</h2><p className="muted">Theo dõi những ngày đến trường của bé</p><p className="legend"><span /> Có mặt <span /> Vắng mặt</p><div className="attendance-chart"><div className="bars" role="img" aria-label="Tỷ lệ có mặt 5 ngày: 93,5%; 94,8%; 96%; 93,1%; 95,2%">
          {[93.5, 94.8, 96, 93.1, 95.2].map((value, index) => <div className="bar-column" key={value}><span>{value.toLocaleString('vi-VN')}%</span><div className={`bar ${index === 4 ? 'current' : ''}`} style={{ height: `${value}%` }} /><small>{['27/10', '28/10', '29/10', '30/10', '02/11'][index]}</small></div>)}
        </div><div className="today"><p className="muted">HÔM NAY · MẪU</p><div className="donut"><strong>95,2%<small>đã có mặt</small></strong></div><div className="attendance-totals"><span>236<small>Có mặt</small></span><span>12<small>Vắng mặt</small></span></div></div></div></section>
        <section className="card"><h2>Sự kiện sắp tới</h2>{[
          ['05', 'Bé khám phá thiên nhiên', '08:30 – 10:00 · Vườn trường', 'Ngoại khóa', 'green'],
          ['10', 'Khám sức khỏe định kỳ', '08:00 – 11:00 · Phòng y tế', 'Sức khỏe', 'blue'],
          ['20', 'Tri ân người gieo mầm', '08:30 – 10:30 · Hội trường', 'Lễ hội', 'amber']
        ].map(([day, title, time, type, tone]) => <div className="event-row" key={day}><div className={`event-date ${tone}`}>{day}<small>TH 11</small></div><div><h3>{title}</h3><p className="muted">{time}</p><small>{type}</small></div></div>)}</section>
        <section className="card"><h2>Hoạt động gần đây</h2><p className="muted section-caption">DỮ LIỆU MẪU · KHÔNG PHẢI HOẠT ĐỘNG THẬT</p>{[
          ['Đã hoàn tất điểm danh buổi sáng', '12/12 lớp đã cập nhật · 236 học sinh có mặt', '08:15'],
          ['Cô Thu Hà chia sẻ khoảnh khắc của bé', 'Lớp Lá 1 · 12 ảnh hoạt động “Sắc màu mùa thu”', '08:05'],
          ['Chào đón bé Trần Minh An', 'Hồ sơ nhập học đã được duyệt · Lớp Mầm 2', '07:50'],
          ['Đã gửi thực đơn tuần đến phụ huynh', 'Thực đơn 02 – 06/11 · Gửi đến 248 phụ huynh', '07:30']
        ].map(([title, detail, time]) => <div className="activity-row" key={title}><span className="activity-mark" aria-hidden="true" /><div><h3>{title}</h3><p className="muted">{detail}</p></div><time>{time}</time></div>)}</section>
        <section className="card"><h2>Các nhóm lớp</h2><div className="group-strip"><i /><i /><i /><i /></div><p className="muted">248 học sinh trong 12 lớp học</p>{previewGroups.map(group => <div className="group-summary" key={group.name}><div><h3>{group.name}</h3><p className="muted">{group.age} · {group.sizes.length} lớp</p></div><span>{group.total} <small>bé</small></span></div>)}</section>
      </div>}
  </>;
}
