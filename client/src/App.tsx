import { useEffect, useState, type FormEvent } from 'react';
import { clearSession, getAccessToken, setSession, request, mutate, responseMessage } from './lib/api';

type HealthStatus = {
  status: string;
  timestamp: string;
  database: string;
};

type UserInfo = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
};

type TenantContext = {
  schoolId: string;
  membershipId: string;
  roles: string[];
};

type SchoolYear = {
  id: string;
  schoolId: string;
  name: string;
  startDate: string;
  endDate: string;
};

type Classroom = {
  id: string;
  schoolId: string;
  schoolYearId: string;
  name: string;
};

export default function App() {
  const [health, setHealth] = useState<HealthStatus | null>(null);
  const [user, setUser] = useState<UserInfo | null>(null);
  const [tenant, setTenant] = useState<TenantContext | null>(null);
  const [schoolYears, setSchoolYears] = useState<SchoolYear[]>([]);
  const [classrooms, setClassrooms] = useState<Classroom[]>([]);
  const [loading, setLoading] = useState(true);
  const [schoolLoading, setSchoolLoading] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [yearName, setYearName] = useState('');
  const [yearStartDate, setYearStartDate] = useState('');
  const [yearEndDate, setYearEndDate] = useState('');
  const [className, setClassName] = useState('');
  const [classSchoolYearId, setClassSchoolYearId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/health/ready', { credentials: 'include' })
      .then(async (response) => {
        const body = await response.json() as HealthStatus;
        setHealth(body);
      })
      .catch(() => setHealth(null))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    const ended = () => {
      setUser(null); setTenant(null); setSchoolYears([]); setClassrooms([]);
      setNotice(null); setError('Session ended. Please sign in again.');
    };
    addEventListener('auth-session-ended', ended);
    return () => removeEventListener('auth-session-ended', ended);
  }, []);

  const loadSchoolData = async () => {
    const session = getAccessToken();
    setSchoolLoading(true);
    setError(null);
    try {
      const currentTenant = await request<TenantContext>('/api/tenant/context');
      const [years, classes] = await Promise.all([
        request<SchoolYear[]>('/api/school-years', currentTenant.schoolId),
        request<Classroom[]>('/api/classes', currentTenant.schoolId)
      ]);
      if (session !== getAccessToken()) return;
      setTenant(currentTenant);
      setSchoolYears(years);
      setClassrooms(classes);
      setClassSchoolYearId((current) => current || years[0]?.id || '');
    } catch (requestError) {
      if (session !== getAccessToken()) return;
      setTenant(null);
      setSchoolYears([]);
      setClassrooms([]);
      setError(requestError instanceof Error ? requestError.message : 'Unable to load school data.');
    } finally {
      setSchoolLoading(false);
    }
  };

  const handleLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setNotice(null);
    try {
      const response = await mutate('/api/auth/login', { email, password });
      const payload = await response.json() as { user: UserInfo; accessToken: string; expiresAtUtc: string };
      setSession(payload.accessToken, payload.expiresAtUtc);
      setUser(payload.user);
      setPassword('');
      await loadSchoolData();
    } catch (requestError) {
      clearSession();
      setUser(null);
      setError(requestError instanceof Error ? requestError.message : 'Sign in failed.');
    }
  };

  const handleLogout = async () => {
    setError(null);
    try {
      await mutate('/api/auth/logout');
      clearSession();
      setError(null);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Sign out failed.');
    }
  };

  const handleCreateSchoolYear = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!tenant) {
      return;
    }

    setError(null);
    setNotice(null);
    try {
      const response = await mutate('/api/school-years',
        { name: yearName, startDate: yearStartDate, endDate: yearEndDate }, tenant.schoolId);
      if (!response.ok) {
        throw new Error(await responseMessage(response));
      }

      setYearName('');
      setYearStartDate('');
      setYearEndDate('');
      setNotice('School year created.');
      await loadSchoolData();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to create school year.');
    }
  };

  const handleCreateClass = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!tenant) {
      return;
    }

    setError(null);
    setNotice(null);
    try {
      const response = await mutate('/api/classes',
        { name: className, schoolYearId: classSchoolYearId }, tenant.schoolId);
      if (!response.ok) {
        throw new Error(await responseMessage(response));
      }

      setClassName('');
      setNotice('Class created.');
      await loadSchoolData();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to create class.');
    }
  };

  return (
    <main className="shell">
      <section className="panel hero">
        <p className="eyebrow">Preschool Connect</p>
        <h1>School operations, communication, and family visibility</h1>
        <p className="subtitle">
          Secure same-origin access for school admins, teachers, and parent accounts.
        </p>
      </section>

      <section className="panel grid">
        <div>
          <h2>System status</h2>
          {loading ? (
            <p>Checking API and database readiness…</p>
          ) : health ? (
            <ul className="status-list">
              <li>Status: {health.status}</li>
              <li>Database: {health.database}</li>
              <li>Checked: {new Date(health.timestamp).toLocaleString()}</li>
            </ul>
          ) : (
            <p role="status">Health check unavailable.</p>
          )}
        </div>

        <div>
          <h2>Sign in</h2>
          <form onSubmit={handleLogin}>
            <label>
              Email
              <input
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </label>
            <label>
              Password
              <input
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </label>
            <button type="submit">Sign in</button>
          </form>
        </div>
      </section>

      {error && <p className="message error" role="alert">{error}</p>}
      {notice && <p className="message success" role="status">{notice}</p>}

      <section className="panel">
        <h2>Authenticated session</h2>
        {user ? (
          <div>
            <p>Signed in as {user.fullName}</p>
            <p>Email: {user.email}</p>
            <p>Identity roles: {user.roles.join(', ') || 'None'}</p>
            <p>Active account: {user.isActive ? 'Yes' : 'No'}</p>
            <button type="button" onClick={() => void handleLogout()}>Sign out</button>
          </div>
        ) : (
          <p>No active user session.</p>
        )}
      </section>

      {user && (
        <section className="panel">
          <h2>School setup</h2>
          {schoolLoading ? (
            <p role="status">Loading school workspace…</p>
          ) : tenant ? (
            <>
              <p>Active school: {tenant.schoolId}</p>
              {tenant.roles.includes('SchoolAdmin') ? (
                <div className="grid">
                  <form onSubmit={handleCreateSchoolYear}>
                    <h3>Create school year</h3>
                    <label>
                      Name
                      <input required maxLength={100} value={yearName} onChange={(event) => setYearName(event.target.value)} />
                    </label>
                    <label>
                      Start date
                      <input type="date" required value={yearStartDate} onChange={(event) => setYearStartDate(event.target.value)} />
                    </label>
                    <label>
                      End date
                      <input type="date" required value={yearEndDate} onChange={(event) => setYearEndDate(event.target.value)} />
                    </label>
                    <button type="submit">Save school year</button>
                  </form>

                  <form onSubmit={handleCreateClass}>
                    <h3>Create class</h3>
                    <label>
                      School year
                      <select
                        required
                        value={classSchoolYearId}
                        onChange={(event) => setClassSchoolYearId(event.target.value)}
                      >
                        <option value="" disabled>Select a school year</option>
                        {schoolYears.map((year) => (
                          <option key={year.id} value={year.id}>{year.name}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Class name
                      <input required maxLength={100} value={className} onChange={(event) => setClassName(event.target.value)} />
                    </label>
                    <button type="submit" disabled={schoolYears.length === 0}>Save class</button>
                  </form>
                </div>
              ) : (
                <p>You can view school setup, but only a school administrator can create school years or classes.</p>
              )}

              <div className="grid">
                <div>
                  <h3>School years</h3>
                  {schoolYears.length > 0 ? (
                    <ul className="status-list">
                      {schoolYears.map((year) => (
                        <li key={year.id}>{year.name}: {year.startDate} – {year.endDate}</li>
                      ))}
                    </ul>
                  ) : <p>No school years yet.</p>}
                </div>
                <div>
                  <h3>Classes</h3>
                  {classrooms.length > 0 ? (
                    <ul className="status-list">
                      {classrooms.map((classroom) => (
                        <li key={classroom.id}>{classroom.name}</li>
                      ))}
                    </ul>
                  ) : <p>No classes yet.</p>}
                </div>
              </div>
            </>
          ) : (
            <p>Your account does not have an active school membership.</p>
          )}
        </section>
      )}
    </main>
  );
}
