import { useCallback, useEffect, useState, type FormEvent, type MouseEvent } from 'react';
import { EmptyState, Message } from './components/ui';
import Classes from './features/classes/Classes';
import Dashboard from './features/dashboard/Dashboard';
import SchoolForm from './features/school/SchoolForm';
import SchoolYears from './features/school/SchoolYears';
import PreviewRoutes from './features/PreviewRoutes';
import Teachers from './features/people/Teachers';
import Students from './features/people/Students';
import { RouteLink } from './components/design';
import { isKnownPagePath } from './lib/preview';
import { clearSession, getAccessToken, setSession, errorMessage, mutate, request, type Classroom, type SchoolYear, type TenantContext, type UserInfo } from './lib/api';

const navigation = [
  ['dashboard', 'Tổng quan'], ['students', 'Học sinh'], ['classes', 'Lớp học'],
  ['teachers', 'Giáo viên'], ['attendance', 'Điểm danh'], ['events', 'Lịch & sự kiện'],
  ['announcements', 'Thông báo'], ['fees', 'Học phí'], ['reports', 'Báo cáo']
];
const implemented = new Set(['dashboard', 'classes', 'settings', 'students', 'teachers', 'attendance', 'events', 'announcements', 'fees', 'reports', 'permissions']);

export default function App() {
  const [path, setPath] = useState(location.pathname);
  const preview = path.startsWith('/preview/');
  const section = path.split('/')[2] ?? 'dashboard';
  const [user, setUser] = useState<UserInfo | null>(null);
  const [tenant, setTenant] = useState<TenantContext | null>(null);
  const [years, setYears] = useState<SchoolYear[]>([]);
  const [classes, setClasses] = useState<Classroom[]>([]);
  const [loading, setLoading] = useState(true);
  const [schoolLoading, setSchoolLoading] = useState(false);
  const [error, setError] = useState('');
  const [health, setHealth] = useState('Đang kiểm tra hệ thống…');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState('');
  const [mobileOpen, setMobileOpen] = useState(false);

  function navigate(url: string) {
    history.pushState(null, '', url);
    setPath(url);
    setSearch('');
    setMobileOpen(false);
    requestAnimationFrame(() => {
      document.getElementById('main-content')?.focus();
      window.scrollTo(0, 0);
    });
  }

  function follow(event: MouseEvent<HTMLAnchorElement>, url: string) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    navigate(url);
  }

  useEffect(() => {
    const ended = () => {
      setUser(null); setTenant(null); setYears([]); setClasses([]);
      setError('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
    };
    addEventListener('auth-session-ended', ended);
    return () => removeEventListener('auth-session-ended', ended);
  }, []);

  useEffect(() => {
    const handlePop = () => { setPath(location.pathname); setSearch(''); setMobileOpen(false); };
    addEventListener('popstate', handlePop);
    return () => removeEventListener('popstate', handlePop);
  }, []);

  const loadSchool = useCallback(async () => {
    const session = getAccessToken();
    setSchoolLoading(true);
    setError('');
    try {
      const context = await request<TenantContext>('/api/tenant/context');
      const staff = context.roles.includes('SchoolAdmin') || context.roles.includes('Teacher');
      const [schoolYears, classrooms] = staff ? await Promise.all([
        request<SchoolYear[]>('/api/school-years', context.schoolId),
        request<Classroom[]>('/api/classes', context.schoolId)
      ]) : [[], []];
      if (session !== getAccessToken()) return;
      setTenant(context);
      setYears(schoolYears);
      setClasses(classrooms);
    } catch (requestError) {
      if (session === getAccessToken()) {
        setTenant(null);
        setYears([]);
        setClasses([]);
        setError(errorMessage(requestError));
      }
      throw requestError;
    } finally {
      setSchoolLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    if (preview) { setLoading(false); return; }
    async function restore() {
      try {
        if (!getAccessToken()) { if (active) setUser(null); return; }
        const current = await request<UserInfo>('/api/auth/me');
        if (active) { setUser(current); await loadSchool(); }
      } catch (requestError) {
        if (active) setError(errorMessage(requestError));
      } finally {
        if (active) setLoading(false);
      }
    }
    void restore();
    return () => { active = false; };
  }, [preview, loadSchool]);

  useEffect(() => {
    if (preview) return;
    let active = true;
    request<{ status: string; database: string }>('/api/health/ready')
      .then(result => { if (active) setHealth(`API: ${result.status} · Cơ sở dữ liệu: ${result.database}`); })
      .catch(requestError => { if (active) setHealth(`Hệ thống chưa sẵn sàng: ${errorMessage(requestError)}`); });
    return () => { active = false; };
  }, [preview]);

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      const response = await mutate('/api/auth/login', { email, password });
      const payload = await response.json() as { user: UserInfo; accessToken: string; expiresAtUtc: string };
      setSession(payload.accessToken, payload.expiresAtUtc);
      setUser(payload.user);
      setPassword('');
      navigate('/admin/dashboard');
      await loadSchool();
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setBusy(false);
    }
  }

  async function logout() {
    setBusy(true);
    setError('');
    try {
      await mutate('/api/auth/logout');
      clearSession();
      setError('');
      setUser(null); setTenant(null); setYears([]); setClasses([]);
      navigate('/login');
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setBusy(false);
    }
  }

  if (loading) return <main className="login-page"><p role="status">Đang khôi phục phiên đăng nhập…</p></main>;
  if (!preview && !user) return <main className="login-page"><section className="card login-card"><p className="brand-word">preschool <span>CONNECT</span></p><h1>Chào mừng trở lại</h1><p className="muted">Đăng nhập bằng tài khoản do nhà trường cấp.</p>{error && <Message error>{error}</Message>}<form onSubmit={login}><fieldset disabled={busy}><legend className="sr-only">Đăng nhập</legend><label>Email<input type="email" autoComplete="username" required value={email} onChange={event => setEmail(event.target.value)} /></label><label>Mật khẩu<input type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /></label><button type="submit">{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button></fieldset></form><hr /><a href="/preview/dashboard" onClick={event => follow(event, '/preview/dashboard')}>Xem trước giao diện với dữ liệu mẫu →</a><p className="muted">Bản xem trước không truy cập hay lưu dữ liệu trường.</p><p className="muted" role="status">{health}</p></section></main>;

  const prefix = preview ? '/preview' : '/admin';
  const pageTitle = section === 'settings' ? 'Cài đặt' : section === 'permissions' ? 'Phân quyền & tài khoản' : navigation.find(([key]) => key === section)?.[1] ?? 'Không tìm thấy';
  const deepPage = path.split('/').filter(Boolean).length > 2;
  const canCreate = tenant?.roles.includes('SchoolAdmin') ?? false;
  const fullName = preview ? 'cô Mai Lan' : user?.fullName || 'bạn';

  return <div className="app-shell" onKeyDown={event => { if (event.key === 'Escape') setMobileOpen(false); }}>
    <a className="skip-link" href="#main-content">Bỏ qua điều hướng</a>
    <aside className={`sidebar ${mobileOpen ? 'is-open' : ''}`} aria-label="Điều hướng chính">
      <a className="brand-word" href={`${prefix}/dashboard`} onClick={event => follow(event, `${prefix}/dashboard`)}>preschool<span>CONNECT</span></a>
      <div className="school-switch"><strong>{preview ? 'Mầm non Hoa Ban' : 'Không gian trường'}</strong><small>{preview ? 'Năm học 2026–2027 · Mẫu' : years.at(-1)?.name ?? 'Chưa có năm học'}</small></div>
      <p className="nav-caption">QUẢN LÝ TRƯỜNG</p>
      <nav>{navigation.map(([key, title]) => <a key={key} href={`${prefix}/${key}`} aria-current={section === key ? 'page' : undefined} onClick={event => follow(event, `${prefix}/${key}`)}>{title}</a>)}</nav>
      <div className="sidebar-bottom"><section className="support-card"><strong>Luôn đồng hành</strong><p>Cần hỗ trợ quản lý trường? Liên hệ người quản trị tài khoản của bạn.</p></section><a className="settings-link" href={`${prefix}/settings`} aria-current={section === 'settings' ? 'page' : undefined} onClick={event => follow(event, `${prefix}/settings`)}>Cài đặt</a><p className="system-status">{preview ? 'Bản xem trước · không kết nối API' : health}</p></div>
    </aside>
    <div className="workspace">
      <header className="topbar"><button className="secondary menu-toggle" aria-label="Đóng/mở điều hướng" aria-expanded={mobileOpen} onClick={() => setMobileOpen(!mobileOpen)}>☰</button><nav aria-label="Đường dẫn" className="breadcrumbs"><a href={`${prefix}/dashboard`} onClick={event => follow(event, `${prefix}/dashboard`)}>Trang chủ</a><span aria-hidden="true">›</span><span>{pageTitle}</span></nav><div className="topbar-tools">{path === `${prefix}/classes` && <label className="search-field"><span className="sr-only">Tìm lớp học</span><input type="search" placeholder="Tìm lớp học…" value={search} onChange={event => setSearch(event.target.value)} /></label>}<details className="profile-menu"><summary aria-label="Menu tài khoản"><span className="avatar">{(preview ? 'ML' : user?.fullName.slice(0, 2))?.toUpperCase()}</span><span>{preview ? 'Nguyễn Mai Lan' : user?.fullName}<small>{preview ? 'Quản trị viên · Mẫu' : tenant?.roles.join(', ') || 'Tài khoản'}</small></span></summary><div className="profile-panel">{preview ? <a href="/login" onClick={event => follow(event, '/login')}>Thoát xem trước / Đăng nhập</a> : <><p>{user?.email}</p><button disabled={busy} onClick={() => void logout()}>{busy ? 'Đang xử lý…' : 'Đăng xuất'}</button></>}</div></details></div></header>
      <main id="main-content" className="page-content" tabIndex={-1}>
        {preview && <Message>BẢN XEM TRƯỚC · Tất cả số liệu và tên là dữ liệu mẫu, không phải dữ liệu trường thật. Không lưu thay đổi.</Message>}
        {deepPage && <RouteLink className="back-link" navigate={navigate} to={`${prefix}/${section}`}>← {pageTitle}</RouteLink>}
        {error && <Message error>{error}</Message>}
        {!isKnownPagePath(path) ? <EmptyState title="Không tìm thấy trang">Đường dẫn không hợp lệ. Chọn một mục trong điều hướng.</EmptyState> :
          schoolLoading && !tenant ? <p role="status" className="card">Đang tải dữ liệu trường…</p> : !preview && !tenant ? <section className="card"><EmptyState title="Không tải được không gian trường">Tài khoản cần có trường và quyền truy cập hợp lệ. Nếu có nhiều trường, việc chọn trường hiện chưa được hỗ trợ.</EmptyState><button onClick={() => { setError(''); void loadSchool().catch(requestError => setError(errorMessage(requestError))); }}>Thử lại</button></section> :
          section === 'dashboard' ? <Dashboard preview={preview} classCount={preview ? 12 : classes.length} fullName={fullName} navigate={navigate} /> :
          preview && (section !== 'classes' || deepPage) ? <PreviewRoutes key={path} path={path} navigate={navigate} /> :
          section === 'teachers' && tenant ? <Teachers key={`${tenant.schoolId}:${path}`} path={path} schoolId={tenant.schoolId}
            canManage={canCreate} classrooms={classes} navigate={navigate} onAssignmentsChanged={loadSchool} /> :
          section === 'students' && tenant ? <Students key={`${tenant.schoolId}:${path}`} path={path} schoolId={tenant.schoolId}
            canManage={canCreate} classrooms={classes} years={years} navigate={navigate} /> :
          section === 'classes' && path.endsWith('/new') ? preview || !canCreate || !tenant ? <Message error>Không thể tạo lớp trong bản mẫu hoặc khi chưa có quyền quản trị.</Message> : <SchoolForm key="class" kind="class" schoolId={tenant.schoolId} years={years} onSaved={loadSchool} /> :
          section === 'classes' && !deepPage ? <Classes preview={preview} classrooms={classes} years={years} search={search} onSearch={setSearch} canCreate={canCreate} onCreate={() => navigate(`${prefix}/classes/new`)} navigate={navigate} /> :
          section === 'settings' && tenant ? <SchoolYears preview={false} schoolId={tenant.schoolId} years={years} classrooms={classes} canManage={canCreate} onSaved={loadSchool} navigate={navigate} /> :
          <section className="card"><EmptyState title={`${pageTitle}: chưa có API nghiệp vụ`}>Không có dữ liệu thật hoặc thao tác lưu cho chức năng này. Bản xem trước tách biệt chỉ chứa dữ liệu tổng hợp minh họa.</EmptyState>{implemented.has(section) && <RouteLink className="button-link" to={`/preview/${section}`} navigate={navigate}>Mở bản xem trước {pageTitle.toLocaleLowerCase('vi-VN')}</RouteLink>}</section>}
        {!preview && section === 'settings' && <p className="settings-access"><RouteLink navigate={navigate} to="/admin/permissions">Phân quyền & tài khoản</RouteLink> · Chưa có API quản trị tài khoản. <RouteLink navigate={navigate} to="/preview/settings">Xem giao diện cài đặt mẫu</RouteLink></p>}
      </main>
    </div>
  </div>;
}
