import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { getAccessToken, setSession, clearSession, request, mutate } from '../src/lib/api.ts';

const originalDispatch = globalThis.dispatchEvent;
const originalFetch = globalThis.fetch;
let events = [];
globalThis.dispatchEvent = event => { events.push(event.type); return true; };
afterEach(() => { clearSession(); events = []; globalThis.fetch = originalFetch; });
process.on('exit', () => { globalThis.dispatchEvent = originalDispatch; });

test('session token is held in memory and explicitly cleared', () => {
  assert.equal(getAccessToken(), null);
  setSession('synthetic-token', new Date(Date.now() + 60_000).toISOString());
  assert.equal(getAccessToken(), 'synthetic-token');
  clearSession();
  assert.equal(getAccessToken(), null);
  assert.deepEqual(events, ['auth-session-ended']);
});

test('expiry clears token and notifies the application', async () => {
  setSession('synthetic-token', new Date(Date.now() + 20).toISOString());
  await new Promise(resolve => setTimeout(resolve, 50));
  assert.equal(getAccessToken(), null);
  assert.deepEqual(events, ['auth-session-ended']);
});

test('replacing a token cancels the previous expiry timer', async () => {
  setSession('old-synthetic-token', new Date(Date.now() + 20).toISOString());
  setSession('new-synthetic-token', new Date(Date.now() + 60_000).toISOString());
  await new Promise(resolve => setTimeout(resolve, 50));
  assert.equal(getAccessToken(), 'new-synthetic-token');
  assert.deepEqual(events, []);
});

test('expired, empty or malformed sessions are rejected', () => {
  assert.throws(() => setSession('', new Date(Date.now() + 60_000).toISOString()));
  assert.throws(() => setSession('synthetic-token', 'not-a-date'));
  assert.throws(() => setSession('synthetic-token', new Date(Date.now() - 1).toISOString()));
  assert.equal(getAccessToken(), null);
});

test('reads and mutations send bearer plus existing school and CSRF headers', async () => {
  setSession('synthetic-token', new Date(Date.now() + 60_000).toISOString());
  const calls = [];
  globalThis.fetch = async (path, options) => {
    calls.push({ path, ...options });
    return Response.json(path === '/api/auth/csrf' ? { requestToken: 'synthetic-csrf' } : []);
  };
  await request('/api/classes', 'synthetic-school');
  await mutate('/api/classes', { name: 'Synthetic' }, 'synthetic-school');
  assert.equal(calls.length, 3);
  for (const call of calls) {
    assert.equal(call.credentials, 'include');
    assert.equal(call.headers.Authorization, 'Bearer synthetic-token');
  }
  assert.equal(calls[0].headers['X-School-Id'], 'synthetic-school');
  assert.equal(calls[2].headers['X-School-Id'], 'synthetic-school');
  assert.equal(calls[2].headers['X-CSRF-TOKEN'], 'synthetic-csrf');
});

test('server 401 clears active session, but 403 preserves it', async () => {
  setSession('synthetic-token', new Date(Date.now() + 60_000).toISOString());
  globalThis.fetch = async () => new Response(null, { status: 403 });
  await assert.rejects(request('/api/classes'));
  assert.equal(getAccessToken(), 'synthetic-token');
  globalThis.fetch = async () => new Response(null, { status: 401 });
  await assert.rejects(request('/api/auth/me'));
  assert.equal(getAccessToken(), null);
  assert.deepEqual(events, ['auth-session-ended']);
});

test('in-flight protected data cannot return after logout', async () => {
  setSession('synthetic-token', new Date(Date.now() + 60_000).toISOString());
  let resolveResponse;
  globalThis.fetch = () => new Promise(resolve => { resolveResponse = resolve; });
  const pending = request('/api/classes');
  clearSession();
  resolveResponse(Response.json([{ name: 'Synthetic private data' }]));
  await assert.rejects(pending);
});

test('an old unauthorized response cannot clear a new session', async () => {
  setSession('old-synthetic-token', new Date(Date.now() + 60_000).toISOString());
  let resolveResponse;
  globalThis.fetch = () => new Promise(resolve => { resolveResponse = resolve; });
  const pending = request('/api/auth/me');
  setSession('new-synthetic-token', new Date(Date.now() + 60_000).toISOString());
  resolveResponse(new Response(null, { status: 401 }));
  await assert.rejects(pending);
  assert.equal(getAccessToken(), 'new-synthetic-token');
  assert.deepEqual(events, []);
});
