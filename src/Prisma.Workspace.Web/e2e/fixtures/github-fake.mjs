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
//   PUT  /__fixture/read-token                         { token, repos: [full_name], status? } token de leitura do modo consulta
//                                                      (HU-0002); status 401/403 faz o GitHub recusar o token em toda chamada
//   PUT  /__fixture/branches?repo=<full_name>          [ "nome-da-branch", ... ] branches que existem no repositório
//
// Modo consulta (token de leitura, só GET): GET /repos/{dono}/{repo}, GET /repos/{dono}/{repo}/branches/{nome}
// e as mesmas leituras de PR da App, com o token cadastrado em /__fixture/read-token.
//   PUT  /__fixture/app-permissions                    { pull_requests: "read" | "write" } (padrão "read": a App ainda sem escrita)
//   POST /__fixture/pr-failure                         { kind: already_exists|nothing_to_compare|head_missing|draft_unsupported|invalid, times? }
//   GET  /__fixture/created                            PRs criados por POST /repos/{r}/pulls (título, head, base, draft, corpo)
//
// HU-0007 (abrir PR pelo card): POST /repos/{r}/pulls exige token com pull_requests:write, que só sai quando a
// fixture concede a escrita; o filtro head=dono:branch da lista funciona como na API real.
import { createServer } from 'node:http';
import { createPublicKey, createVerify, randomBytes } from 'node:crypto';

const port = Number(process.env.GITHUB_FAKE_PORT ?? 3134);
const appId = process.env.GITHUB_FAKE_APP_ID ?? '';
const publicKey = process.env.GITHUB_FAKE_APP_PUBLIC_KEY ? createPublicKey(process.env.GITHUB_FAKE_APP_PUBLIC_KEY) : null;

let state;
function newRepo(r) {
  return { id: Number(r.id), full_name: r.full_name, default_branch: r.default_branch ?? 'main',
    installation_id: Number(r.installation_id ?? 1), pulls: new Map(), reviews: new Map(), branches: new Set() };
}
function reset() {
  const initial = newRepo({ id: 900001, full_name: 'qa/metodo', default_branch: 'main', installation_id: 1 });
  state = { repos: new Map([[initial.full_name, initial]]), current: initial.full_name, failures: [], calls: [], tokens: new Map(), readTokens: new Map(),
    grant: { pull_requests: 'read' }, prFailures: [], created: [] };
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

// Token de leitura (PAT fine-grained) do modo consulta: acesso só aos repositórios cadastrados, resolvidos na hora.
function readToken(auth) {
  const m = /^(?:Bearer|token)\s+(\S+)$/i.exec(auth ?? '');
  const t = m && state.readTokens.get(m[1]);
  if (!t) return null;
  return { status: t.status, repoIds: new Set([...t.repos].map(name => state.repos.get(name)?.id).filter(Boolean)) };
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

// Respostas 422 no formato da API real (POST /repos/{owner}/{repo}/pulls).
const PR_FAILURES = {
  already_exists: head => ({ message: 'Validation Failed', errors: [{ resource: 'PullRequest', code: 'custom', message: `A pull request already exists for ${head}.` }] }),
  nothing_to_compare: (head, base) => ({ message: 'Validation Failed', errors: [{ resource: 'PullRequest', code: 'custom', message: `No commits between ${base} and ${head}` }] }),
  head_missing: () => ({ message: 'Validation Failed', errors: [{ resource: 'PullRequest', field: 'head', code: 'invalid' }] }),
  draft_unsupported: () => ({ message: 'Validation Failed', errors: [{ resource: 'PullRequest', code: 'custom', message: 'Draft pull requests are not supported in this repository.' }] }),
  invalid: () => ({ message: 'Validation Failed', errors: [{ resource: 'PullRequest', field: 'title', code: 'missing_field' }] }),
};

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
      if (req.method === 'PUT' && path === '/__fixture/read-token') {
        const t = await body(req);
        state.readTokens.set(String(t.token), { repos: new Set(t.repos ?? []), status: t.status ? Number(t.status) : null });
        return json(res, 204);
      }
      if (req.method === 'PUT' && path === '/__fixture/branches') {
        const repo = fixtureRepo(url); if (!repo) return json(res, 404, { message: 'repositório desconhecido na fixture' });
        repo.branches = new Set(await body(req)); return json(res, 200, [...repo.branches]);
      }
      if (req.method === 'POST' && path === '/__fixture/fail') { const f = await body(req); state.failures.push({ ...f, re: new RegExp(f.match), times: f.times ?? 1 }); return json(res, 204); }
      if (req.method === 'GET' && path === '/__fixture/calls') return json(res, 200, state.calls);
      if (req.method === 'PUT' && path === '/__fixture/app-permissions') {
        const g = await body(req);
        if (!['read', 'write'].includes(g.pull_requests)) return json(res, 400, { message: 'pull_requests deve ser read ou write' });
        state.grant = { pull_requests: g.pull_requests }; return json(res, 200, state.grant);
      }
      if (req.method === 'POST' && path === '/__fixture/pr-failure') {
        const f = await body(req);
        if (!PR_FAILURES[f.kind]) return json(res, 400, { message: 'tipo de falha desconhecido' });
        state.prFailures.push({ kind: f.kind, times: f.times ?? 1, repo: f.repo ?? null }); return json(res, 204);
      }
      if (req.method === 'GET' && path === '/__fixture/created') return json(res, 200, state.created);
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
      // Só leitura de PRs e metadados; escrita de PRs apenas quando a fixture concede (a App real precisa do aceite da instalação).
      const proibidas = Object.entries(perms).filter(([k, v]) => !((k === 'metadata' && v === 'read')
        || (k === 'pull_requests' && (v === 'read' || (v === 'write' && state.grant.pull_requests === 'write')))));
      if (proibidas.length) return json(res, 422, { message: `permissão não concedida à App de teste: ${proibidas.map(([k, v]) => `${k}:${v}`).join(',')}` });
      const allowed = pedido.repository_ids ? pedido.repository_ids.map(Number) : repos.map(r => r.id);
      if (!allowed.every(id => repos.some(r => r.id === id))) return json(res, 422, { message: 'repositório fora da instalação' });
      const token = `ghs_fake_${randomBytes(18).toString('hex')}`;
      state.tokens.set(token, { expires: Date.now() + 3600_000, repoIds: new Set(allowed), write: perms.pull_requests === 'write' });
      return json(res, 201, { token, expires_at: new Date(Date.now() + 3600_000).toISOString(), permissions: perms, repository_selection: 'selected' });
    }

    const pat = readToken(req.headers.authorization);
    if (pat?.status) return json(res, pat.status, { message: pat.status === 401 ? 'Bad credentials' : 'Resource not accessible by personal access token' });
    if (pat && req.method !== 'GET') return json(res, 403, { message: 'token de leitura: só GET' });
    const tok = installationToken(req.headers.authorization) ?? pat;
    if (!tok) return json(res, 401, { message: 'Bad credentials' });
    let r = /^\/repos\/([^/]+\/[^/]+)$/.exec(path);
    if (req.method === 'GET' && r) {
      const repo = state.repos.get(decodeURIComponent(r[1]));
      if (!repo || !tok.repoIds.has(repo.id)) return json(res, 404, { message: 'Not Found' });
      return json(res, 200, { id: repo.id, full_name: repo.full_name, default_branch: repo.default_branch, private: true });
    }
    r = /^\/repos\/([^/]+\/[^/]+)\/branches\/(.+)$/.exec(path);
    if (req.method === 'GET' && r) {
      const repo = state.repos.get(decodeURIComponent(r[1]));
      const name = r[2].split('/').map(decodeURIComponent).join('/');
      if (!repo || !tok.repoIds.has(repo.id) || !repo.branches.has(name)) return json(res, 404, { message: 'Branch not found' });
      return json(res, 200, { name, commit: { sha: 'e'.repeat(40) } });
    }

    if (req.method === 'GET' && path === '/installation/repositories') {
      const all = [...state.repos.values()].filter(r => tok.repoIds.has(r.id))
        .map(r => ({ id: r.id, full_name: r.full_name, default_branch: r.default_branch }));
      return json(res, 200, { total_count: all.length, repositories: page(all, url) });
    }
    m = /^\/repos\/([^/]+\/[^/]+)\/pulls(?:\/(\d+)(\/reviews)?)?$/.exec(path);
    const repo = m && state.repos.get(decodeURIComponent(m[1]));
    if (!m || !repo || !tok.repoIds.has(repo.id)) return json(res, 404, { message: 'Not Found' });
    if (req.method === 'POST' && !m[2]) {
      if (!tok.write) return json(res, 403, { message: 'Resource not accessible by integration' });
      const pedido = await body(req);
      const owner = repo.full_name.split('/')[0];
      const head = String(pedido.head ?? ''); const base = String(pedido.base ?? repo.default_branch);
      const falhaPr = state.prFailures.find(f => f.times > 0 && (!f.repo || f.repo === repo.full_name));
      if (falhaPr) { falhaPr.times--; return json(res, 422, PR_FAILURES[falhaPr.kind](`${owner}:${head}`, base)); }
      if (!head || !String(pedido.title ?? '').trim()) return json(res, 422, PR_FAILURES.invalid());
      if ([...repo.pulls.keys()].some(n => { const p = fullPull(repo, n); return p.state === 'open' && p.head.ref === head; }))
        return json(res, 422, PR_FAILURES.already_exists(`${owner}:${head}`));
      const n = Math.max(0, ...repo.pulls.keys()) + 1;
      const agora = new Date().toISOString();
      repo.pulls.set(n, { title: pedido.title, body: pedido.body ?? null, draft: !!pedido.draft, state: 'open',
        head: { ref: head }, base: { ref: base }, created_at: agora, updated_at: agora,
        user: { id: 99_000_001, login: 'prisma-metodo-vitor[bot]', type: 'Bot' } });
      state.created.push({ repo: repo.full_name, number: n, title: pedido.title, head, base, draft: !!pedido.draft, body: pedido.body ?? null });
      return json(res, 201, fullPull(repo, n));
    }
    if (req.method === 'GET' && !m[2]) {
      const st = url.searchParams.get('state') ?? 'open';
      // head=dono:branch (como a API real): só PRs cuja head é essa branch do repositório do dono.
      const headFilter = url.searchParams.get('head');
      // Ordenação como a API real: sort=created (padrão)|updated; direction padrão desc para created, asc nos demais.
      const sort = url.searchParams.get('sort') ?? 'created';
      const direction = url.searchParams.get('direction') ?? (sort === 'created' ? 'desc' : 'asc');
      const key = sort === 'updated' ? 'updated_at' : 'created_at';
      const list = [...repo.pulls.keys()].map(n => fullPull(repo, n))
        .filter(p => st === 'all' || p.state === st)
        .filter(p => !headFilter || (headFilter === `${repo.full_name.split('/')[0]}:${p.head.ref}` && p.head.repo?.id === repo.id))
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
