#!/usr/bin/env node
// GitHub falso para o E2E e o QA da integração Git do método (fase 2a). Sem dependências.
// Implementa só as leituras que o provedor da 2a usa e uma API de controle em /__fixture.
//
// Ambiente:
//   GITHUB_FAKE_PORT            porta (padrão 3134)
//   GITHUB_FAKE_APP_ID          AppId esperado no claim iss do JWT da App
//   GITHUB_FAKE_APP_PUBLIC_KEY  chave pública PEM (SPKI) da App de teste, para conferir o RS256
//
// Controle (somente para testes):
//   POST /__fixture/reset                       limpa PRs, reviews, falhas e chamadas
//   PUT  /__fixture/repository                  { id, full_name, default_branch, installation_id }
//   PUT  /__fixture/pulls/{number}              PR completo ou parcial (mescla com o atual)
//   PUT  /__fixture/pulls/{number}/reviews      [ { id, state, commit_id, submitted_at, user } ]
//   POST /__fixture/fail                        { match: "<regex do caminho>", status, retry_after?, times? }
//   GET  /__fixture/calls                       chamadas recebidas (sem Authorization)
import { createServer } from 'node:http';
import { createPublicKey, createVerify, randomBytes } from 'node:crypto';

const port = Number(process.env.GITHUB_FAKE_PORT ?? 3134);
const appId = process.env.GITHUB_FAKE_APP_ID ?? '';
const publicKey = process.env.GITHUB_FAKE_APP_PUBLIC_KEY ? createPublicKey(process.env.GITHUB_FAKE_APP_PUBLIC_KEY) : null;

let state;
function reset() {
  state = {
    repository: { id: 900001, full_name: 'qa/metodo', default_branch: 'main', installation_id: 1 },
    pulls: new Map(), reviews: new Map(), failures: [], calls: [], tokens: new Map(),
  };
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
  if (claims.exp - claims.iat > 600) return 'validade do JWT acima de 10 minutos';
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

function fullPull(n) {
  const p = state.pulls.get(n);
  const repo = state.repository;
  return {
    id: 7_000_000 + n, number: n, html_url: `https://github.com/${repo.full_name}/pull/${n}`,
    title: `PR ${n}`, state: 'open', draft: false, merged: false, merged_at: null, merge_commit_sha: null,
    created_at: new Date().toISOString(), updated_at: new Date().toISOString(), closed_at: null,
    ...p,
    head: { ref: 'feature/x', sha: 'b'.repeat(40), ...(p.head ?? {}) },
    base: { ref: repo.default_branch, ...(p.base ?? {}), repo: { id: repo.id, full_name: repo.full_name } },
  };
}

async function body(req) { let s = ''; for await (const c of req) s += c; return s ? JSON.parse(s) : {}; }

createServer(async (req, res) => {
  const url = new URL(req.url, 'http://fake');
  const path = url.pathname;
  try {
    if (path.startsWith('/__fixture')) {
      if (req.method === 'POST' && path === '/__fixture/reset') { reset(); return json(res, 204); }
      if (req.method === 'PUT' && path === '/__fixture/repository') { Object.assign(state.repository, await body(req)); return json(res, 200, state.repository); }
      let m = /^\/__fixture\/pulls\/(\d+)$/.exec(path);
      if (req.method === 'PUT' && m) {
        const n = Number(m[1]); const atual = state.pulls.get(n) ?? {}; const novo = await body(req);
        state.pulls.set(n, { ...atual, ...novo, head: { ...(atual.head ?? {}), ...(novo.head ?? {}) }, base: { ...(atual.base ?? {}), ...(novo.base ?? {}) }, updated_at: new Date().toISOString() });
        return json(res, 200, fullPull(n));
      }
      m = /^\/__fixture\/pulls\/(\d+)\/reviews$/.exec(path);
      if (req.method === 'PUT' && m) { state.reviews.set(Number(m[1]), await body(req)); return json(res, 200, state.reviews.get(Number(m[1]))); }
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

    const repo = state.repository;
    let m = /^\/app\/installations\/(\d+)\/access_tokens$/.exec(path);
    if (req.method === 'POST' && m) {
      const erro = verifyAppJwt(req.headers.authorization);
      if (erro) return json(res, 401, { message: erro });
      if (Number(m[1]) !== Number(repo.installation_id)) return json(res, 404, { message: 'instalação desconhecida' });
      const pedido = await body(req);
      const perms = pedido.permissions ?? {};
      const proibidas = Object.entries(perms).filter(([k, v]) => !(['pull_requests', 'metadata'].includes(k) && v === 'read'));
      if (proibidas.length) return json(res, 422, { message: `permissão não concedida à App de teste: ${proibidas.map(([k, v]) => `${k}:${v}`).join(',')}` });
      if (pedido.repository_ids && !pedido.repository_ids.every(id => Number(id) === Number(repo.id))) return json(res, 422, { message: 'repositório fora da instalação' });
      const token = `ghs_fake_${randomBytes(18).toString('hex')}`;
      state.tokens.set(token, { expires: Date.now() + 3600_000 });
      return json(res, 201, { token, expires_at: new Date(Date.now() + 3600_000).toISOString(), permissions: perms, repository_selection: 'selected' });
    }

    if (!installationToken(req.headers.authorization)) return json(res, 401, { message: 'Bad credentials' });

    if (req.method === 'GET' && path === '/installation/repositories') {
      const all = [{ id: repo.id, full_name: repo.full_name, default_branch: repo.default_branch }];
      return json(res, 200, { total_count: all.length, repositories: page(all, url) });
    }
    const base = `/repos/${repo.full_name}`;
    if (!path.startsWith(base + '/') && path !== base) return json(res, 404, { message: 'Not Found' });
    if (req.method === 'GET' && path === `${base}/pulls`) {
      const st = url.searchParams.get('state') ?? 'open';
      const list = [...state.pulls.keys()].sort((a, b) => a - b).map(fullPull)
        .filter(p => st === 'all' || p.state === st)
        .map(({ merged, ...resumo }) => resumo);
      return json(res, 200, page(list, url));
    }
    m = new RegExp(`^${base.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')}/pulls/(\\d+)(/reviews)?$`).exec(path);
    if (req.method === 'GET' && m) {
      const n = Number(m[1]);
      if (!state.pulls.has(n)) return json(res, 404, { message: 'Not Found' });
      if (m[2]) return json(res, 200, page(state.reviews.get(n) ?? [], url));
      return json(res, 200, fullPull(n));
    }
    return json(res, 404, { message: 'Not Found' });
  } catch (e) {
    return json(res, 500, { message: 'erro na fixture', detail: String(e?.message ?? e) });
  }
}).listen(port, '0.0.0.0', () => process.stdout.write(`github-fake ouvindo em ${port}\n`));
