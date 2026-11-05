let token: string | null = null;
let expiryTimer: ReturnType<typeof setTimeout> | undefined;

export function getAccessToken() { return token; }

export function clearSession() {
  token = null;
  clearTimeout(expiryTimer);
  globalThis.dispatchEvent(new Event('auth-session-ended'));
}

export function setSession(accessToken: string, expiresAtUtc: string) {
  const remaining = Date.parse(expiresAtUtc) - Date.now();
  if (!accessToken || !Number.isFinite(remaining) || remaining <= 0)
    throw new Error('Phiên đăng nhập không hợp lệ hoặc đã hết hạn.');
  clearTimeout(expiryTimer);
  token = accessToken;
  expiryTimer = setTimeout(clearSession, remaining);
}

export type UserInfo = { userId: string; fullName: string; email: string; isActive: boolean; roles: string[] };
export type TenantContext = { schoolId: string; membershipId: string; roles: string[] };
export type SchoolYear = { id: string; schoolId: string; name: string; startDate: string; endDate: string; classCount?: number };
export type ClassroomTeacher = { id: string; name: string; role: string };
export type Classroom = { id: string; schoolId: string; schoolYearId: string; name: string;
  room: string; capacity: number | null; studentCount: number; teachers: ClassroomTeacher[] };

export async function responseMessage(response: Response) {
  const text = await response.text();
  if (response.headers.get('content-type')?.includes('json') && text) {
    const body = JSON.parse(text) as { message?: string; title?: string; errors?: Record<string, string[]> };
    if (body.errors) return Object.values(body.errors).flat().join(' ') || body.title || `Yêu cầu thất bại (${response.status}).`;
    return body.message ?? body.title ?? `Yêu cầu thất bại (${response.status}).`;
  }
  return `Yêu cầu thất bại (${response.status}).`;
}

export async function request<T>(path: string, schoolId?: string): Promise<T> {
  const token = getAccessToken();
  const response = await fetch(path, {
    credentials: 'include',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(schoolId ? { 'X-School-Id': schoolId } : {})
    }
  });
  if (response.status === 401 && token === getAccessToken() && token) clearSession();
  if (token !== getAccessToken()) throw new Error('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
  if (!response.ok) throw new Error(await responseMessage(response));
  const result = await response.json() as T;
  if (token !== getAccessToken()) throw new Error('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
  return result;
}

export async function mutate(path: string, data?: object, schoolId?: string, method: 'POST' | 'PUT' | 'DELETE' = 'POST') {
  const token = getAccessToken();
  const { requestToken } = await request<{ requestToken: string }>('/api/auth/csrf');
  if (token !== getAccessToken()) throw new Error('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
  const response = await fetch(path, {
    method,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': requestToken,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(schoolId ? { 'X-School-Id': schoolId } : {})
    },
    body: data ? JSON.stringify(data) : undefined
  });
  if (response.status === 401 && token === getAccessToken() && token) clearSession();
  if (token !== getAccessToken()) throw new Error('Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.');
  if (!response.ok) throw new Error(await responseMessage(response));
  return response;
}

export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Không thể kết nối. Vui lòng thử lại.';
}
