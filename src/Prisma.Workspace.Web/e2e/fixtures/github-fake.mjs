#!/usr/bin/env node
// GitHub falso para o E2E e o QA da integração Git do método (fase 2a). Sem dependências.
// Implementa só as leituras que o provedor da 2a usa e uma API de controle em /__fixture.
// Guarda vários repositórios ao mesmo tempo (um por projeto de teste), cada um com seus PRs.
//
// Ambiente:
//   GITHUB_FAKE_PORT            porta (padrão 3134)
//   GITHUB_FAKE_APP_ID          AppId esperado no claim iss do JWT da App
//   GITHUB_FAKE_APP_PUBLIC_KEY  chave pública PEM (SPKI) da App de teste, para conferir o RS256
//
// Controle (somente para testes):
//   POST /__fixture/reset                              limpa repositórios, PRs, reviews, falhas e chamadas
//   PUT  /__fixture/repository                         { id, full_name, default_branch, installation_id } (cria ou atualiza; vira o atual)
//   PUT  /__fixture/pulls/{number}?repo=<full_name>    PR completo ou parcial (mescla); sem repo usa o atual
//   PUT  /__fixture/pulls/{number}/reviews?repo=...    [ { id, state, commit_id, submitted_at, user } ]
//   POST /__fixture/fail                               { match: "<regex do caminho>", status, retry_after?, times? }
//   GET  /__fixture/calls                              chamadas recebidas (sem Authorization)
import { createServer } from 'node:http';
import { createPublicKey, createVerify, randomBytes } from 'node:crypto';

const port = Number(process.env.GITHUB_FAKE_PORT ?? 3134);
const appId = process.env.GITHUB_FAKE_APP_ID ?? '';
const publicKey = process.env.GITHUB_FAKE_APP_PUBLIC_KEY ? createPublicKey(process.env.GITHUB_FAKE_APP_PUBLIC_KEY) : null;

let state;
function newRepo(r) {
  return { id: Number(r.id), full_name: r.full_name, default_branch: r.default_branch ?? 'main',
    installation_id: Number(r.installation_id ?? 1), pulls: new Map(), reviews: new Map() };
}
function reset() {
  const initial = newRepo({ id: 900001, full_name: 'qa/metodo', default_branch: 'main', installation_id: 1 });
  state = { repos: new Map([[initial.full_name, initial]]), current: initial.full_name, failures: [], calls: [], tokens: new Map() };
}
reset();

const json = (res, status, body, headers = {}) => {
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', ...headers });
  res.end(body === undefined ? '' : JSON.stringify(body));
};
const b64url = s => Buffer.from(s, 'base64url');

function verifyAppJwt(auth) {
  const m = /^Bearer\s+([^.]+)\.([^.]+)\.([^.]+)$/.exec(auth ?? '');
  if (!m) return 'JWT ausente';
  let header, claims;
  try { header = JSON.parse(b64url(m[1])); claims = JSON.parse(b64url(m[2])); } catch { return 'JWT ilegível'; }
  if (header.alg !== 'RS256') return 'alg diferente de RS256';
  if (publicKey) {
    const v = createVerify('RSA-SHA256'); v.update(`${m[1]}.${m[2]}`);
    if (!v.verify(publicKey, b64url(m[3]))) return 'assinatura do JWT inválida';
  }
  const now = Math.floor(Date.now() / 1000);
  if (appId && String(claims.iss) !== appId) return 'iss diferente do AppId';
  if (typeof claims.exp !== 'number' || claims.exp <= now) return 'JWT expirado';
  if (typeof claims.iat !== 'number' || claims.iat > now + 60) return 'iat no futuro';
  // Regra do GitHub: exp no máximo 10 minutos no futuro (iat pode vir atrasado para tolerar relógio).
  if (claims.exp - now > 600) return 'exp mais de 10 minutos no futuro';
  return null;
}

function installationToken(auth) {
  const m = /^(?:Bearer|token)\s+(\S+)$/i.exec(auth ?? '');
  const t = m && state.tokens.get(m[1]);
  return t && t.expires > Date.now() ? t : null;
}

function page(list, url) {
  const per = Math.min(Number(url.searchParams.get('per_page') ?? 30), 100);
  const n = Math.max(Number(url.searchParams.get('page') ?? 1), 1);
  return list.slice((n - 1) * per, n * per);
}

function fullPull(repo, n) {
  const p = repo.pulls.get(n);
  const self = { id: repo.id, full_name: repo.full_name };
  return {
    id: 7_000_000 + n, number: n, html_url: `https://github.com/${repo.full_name}/pull/${n}`,
    title: `PR ${n}`, state: 'open', draft: false, merged: false, merged_at: null, merge_commit_sha: null,
    closed_at: null,
    ...p,
    head: { ref: 'feature/x', sha: 'b'.repeat(40), repo: self, ...(p.head ?? {}) },
    base: { ref: repo.default_branch, ...(p.base ?? {}), repo: self },
  };
}

async function body(req) { let s = ''; for await (const c of req) s += c; return s ? JSON.parse(s) : {}; }
const fixtureRepo = url => state.repos.get(url.searchParams.get('repo') ?? state.current);

createServer(async (req, res) => {
  const url = new URL(req.url, 'http://fake');
  const path = url.pathname;
  try {
    if (path.startsWith('/__fixture')) {
      if (req.method === 'POST' && path === '/__fixture/reset') { reset(); return json(res, 204); }
      if (req.method === 'PUT' && path === '/__fixture/repository') {
        const r = await body(req);
        const repo = state.repos.get(r.full_name) ?? newRepo(r);
        Object.assign(repo, { id: Number(r.id ?? repo.id), default_branch: r.default_branch ?? repo.default_branch,
          installation_id: Number(r.installation_id ?? repo.installation_id) });
        state.repos.set(repo.full_name, repo); state.current = repo.full_name;
        return json(res, 200, { id: repo.id, full_name: repo.full_name, default_branch: repo.default_branch, installation_id: repo.installation_id });
      }
      let m = /^\/__fixture\/pulls\/(\d+)$/.exec(path);
      if (req.method === 'PUT' && m) {
        const repo = fixtureRepo(url); if (!repo) return json(res, 404, { message: 'repositório desconhecido na fixture' });
        const n = Number(m[1]); const atual = repo.pulls.get(n) ?? {}; const novo = await body(req);
        repo.pulls.set(n, { ...atual, ...novo, head: { ...(atual.head ?? {}), ...(novo.head ?? {}) },
          base: { ...(atual.base ?? {}), ...(novo.base ?? {}) },
          created_at: atual.created_at ?? novo.created_at ?? new Date().toISOString(),
          updated_at: novo.updated_at ?? new Date().toISOString() });
        return json(res, 200, fullPull(repo, n));
      }
      m = /^\/__fixture\/pulls\/(\d+)\/reviews$/.exec(path);
      if (req.method === 'PUT' && m) {
        const repo = fixtureRepo(url); if (!repo) return json(res, 404, { message: 'repositório desconhecido na fixture' });
        repo.reviews.set(Number(m[1]), await body(req)); return json(res, 200, repo.reviews.get(Number(m[1])));
      }
      if (req.method === 'POST' && path === '/__fixture/fail') { const f = await body(req); state.failures.push({ ...f, re: new RegExp(f.match), times: f.times ?? 1 }); return json(res, 204); }
      if (req.method === 'GET' && path === '/__fixture/calls') return json(res, 200, state.calls);
      return json(res, 404, { message: 'rota de controle desconhecida' });
    }

    state.calls.push({ method: req.method, path, query: Object.fromEntries(url.searchParams), at: new Date().toISOString(),
      accept: req.headers.accept ?? null, userAgent: req.headers['user-agent'] ?? null, apiVersion: req.headers['x-github-api-version'] ?? null });
    const falha = state.failures.find(f => f.times > 0 && f.re.test(path));
    if (falha) {
      falha.times--;
      const h = falha.retry_after ? { 'retry-after': String(falha.retry_after) } : {};
      if (falha.status === 403 || falha.status === 429) h['x-ratelimit-remaining'] = '0';
      return json(res, falha.status, { message: 'falha simulada' }, h);
    }

    let m = /^\/app\/installations\/(\d+)\/access_tokens$/.exec(path);
    if (req.method === 'POST' && m) {
      const erro = verifyAppJwt(req.headers.authorization);
      if (erro) return json(res, 401, { message: erro });
      const installation = Number(m[1]);
      const repos = [...state.repos.values()].filter(r => r.installation_id === installation);
      if (!repos.length) return json(res, 404, { message: 'instalação desconhecida' });
      const pedido = await body(req);
      const perms = pedido.permissions ?? {};
      const proibidas = Object.entries(perms).filter(([k, v]) => !(['pull_requests', 'metadata'].includes(k) && v === 'read'));
      if (proibidas.length) return json(res, 422, { message: `permissão não concedida à App de teste: ${proibidas.map(([k, v]) => `${k}:${v}`).join(',')}` });
      const allowed = pedido.repository_ids ? pedido.repository_ids.map(Number) : repos.map(r => r.id);
      if (!allowed.every(id => repos.some(r => r.id === id))) return json(res, 422, { message: 'repositório fora da instalação' });
      const token = `ghs_fake_${randomBytes(18).toString('hex')}`;
      state.tokens.set(token, { expires: Date.now() + 3600_000, repoIds: new Set(allowed) });
      return json(res, 201, { token, expires_at: new Date(Date.now() + 3600_000).toISOString(), permissions: perms, repository_selection: 'selected' });
    }

    const tok = installationToken(req.headers.authorization);
    if (!tok) return json(res, 401, { message: 'Bad credentials' });

    if (req.method === 'GET' && path === '/installation/repositories') {
      const all = [...state.repos.values()].filter(r => tok.repoIds.has(r.id))
        .map(r => ({ id: r.id, full_name: r.full_name, default_branch: r.default_branch }));
      return json(res, 200, { total_count: all.length, repositories: page(all, url) });
    }
    m = /^\/repos\/([^/]+\/[^/]+)\/pulls(?:\/(\d+)(\/reviews)?)?$/.exec(path);
    const repo = m && state.repos.get(decodeURIComponent(m[1]));
    if (!m || !repo || !tok.repoIds.has(repo.id)) return json(res, 404, { message: 'Not Found' });
    if (req.method === 'GET' && !m[2]) {
      const st = url.searchParams.get('state') ?? 'open';
      // Ordenação como a API real: sort=created (padrão)|updated; direction padrão desc para created, asc nos demais.
      const sort = url.searchParams.get('sort') ?? 'created';
      const direction = url.searchParams.get('direction') ?? (sort === 'created' ? 'desc' : 'asc');
      const key = sort === 'updated' ? 'updated_at' : 'created_at';
      const list = [...repo.pulls.keys()].map(n => fullPull(repo, n))
        .filter(p => st === 'all' || p.state === st)
        .sort((a, b) => (a[key] < b[key] ? -1 : a[key] > b[key] ? 1 : a.number - b.number) * (direction === 'desc' ? -1 : 1))
        .map(({ merged: _merged, ...resumo }) => resumo);
      return json(res, 200, page(list, url));
    }
    if (req.method === 'GET') {
      const n = Number(m[2]);
      if (!repo.pulls.has(n)) return json(res, 404, { message: 'Not Found' });
      if (m[3]) return json(res, 200, page(repo.reviews.get(n) ?? [], url));
      return json(res, 200, fullPull(repo, n));
    }
    return json(res, 404, { message: 'Not Found' });
  } catch (e) {
    return json(res, 500, { message: 'erro na fixture', detail: String(e?.message ?? e) });
  }
}).listen(port, '0.0.0.0', () => process.stdout.write(`github-fake ouvindo em ${port}\n`));
