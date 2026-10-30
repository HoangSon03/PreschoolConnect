import { FormEvent, useEffect, useState } from 'react';

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

async function responseMessage(response: Response): Promise<string> {
  try {
    const body = await response.json() as { message?: string; title?: string };
    return body.message ?? body.title ?? `Request failed (${response.status}).`;
  } catch {
    return `Request failed (${response.status}).`;
  }
}

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

  const loadSchoolData = async () => {
    setSchoolLoading(true);
    setError(null);
    try {
      const tenantResponse = await fetch('/api/tenant/context', { credentials: 'include' });
      if (!tenantResponse.ok) {
        throw new Error(await responseMessage(tenantResponse));
      }

      const currentTenant = await tenantResponse.json() as TenantContext;
      setTenant(currentTenant);
      const headers = { 'X-School-Id': currentTenant.schoolId };
      const [yearsResponse, classesResponse] = await Promise.all([
        fetch('/api/school-years', { credentials: 'include', headers }),
        fetch('/api/classes', { credentials: 'include', headers })
      ]);
      if (!yearsResponse.ok) {
        throw new Error(await responseMessage(yearsResponse));
      }
      if (!classesResponse.ok) {
        throw new Error(await responseMessage(classesResponse));
      }

      const years = await yearsResponse.json() as SchoolYear[];
      setSchoolYears(years);
      setClassrooms(await classesResponse.json() as Classroom[]);
      setClassSchoolYearId((current) => current || years[0]?.id || '');
    } catch (requestError) {
      setTenant(null);
      setSchoolYears([]);
      setClassrooms([]);
      setError(requestError instanceof Error ? requestError.message : 'Unable to load school data.');
    } finally {
      setSchoolLoading(false);
    }
  };

  const getAntiforgeryToken = async () => {
    const response = await fetch('/api/auth/csrf', { credentials: 'include' });
    if (!response.ok) {
      throw new Error(await responseMessage(response));
    }
    const result = await response.json() as { requestToken: string };
    return result.requestToken;
  };

  const handleLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setNotice(null);
    try {
      const requestToken = await getAntiforgeryToken();
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': requestToken },
        body: JSON.stringify({ email, password })
      });

      if (!response.ok) {
        throw new Error(await responseMessage(response));
      }

      const payload = await response.json() as { user: UserInfo };
      setUser(payload.user);
      setPassword('');
      await loadSchoolData();
    } catch (requestError) {
      setUser(null);
      setError(requestError instanceof Error ? requestError.message : 'Sign in failed.');
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
      const requestToken = await getAntiforgeryToken();
      const response = await fetch('/api/school-years', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-TOKEN': requestToken,
          'X-School-Id': tenant.schoolId
        },
        body: JSON.stringify({ name: yearName, startDate: yearStartDate, endDate: yearEndDate })
      });
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
      const requestToken = await getAntiforgeryToken();
      const response = await fetch('/api/classes', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-TOKEN': requestToken,
          'X-School-Id': tenant.schoolId
        },
        body: JSON.stringify({ name: className, schoolYearId: classSchoolYearId })
      });
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
