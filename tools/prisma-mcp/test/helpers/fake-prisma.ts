// Servidor HTTP falso do Prisma para testar o adaptador sem API real.
// Registra cada requisição (método, rota, cabeçalhos) e responde pelas rotas configuradas;
// rota não configurada devolve 404, para o teste ver qualquer chamada inesperada.
import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import type { AddressInfo } from 'node:net';

export interface Requisicao { method: string; path: string; headers: IncomingMessage['headers']; body: string; /** Caminho com a query. */ url?: string; }
export type Resposta = { status?: number; json?: unknown; text?: string };
export type Rota = (req: Requisicao) => Resposta | Promise<Resposta>;

export interface FakePrisma {
  url: string;
  requisicoes: Requisicao[];
  on(method: string, path: string | RegExp, rota: Rota | Resposta): void;
  escritas(): Requisicao[];
  close(): Promise<void>;
}

export async function startFakePrisma(): Promise<FakePrisma> {
  const rotas: { method: string; path: string | RegExp; rota: Rota }[] = [];
  const requisicoes: Requisicao[] = [];
  const server = createServer(async (req: IncomingMessage, res: ServerResponse) => {
    // Junta os bytes antes de decodificar: um caractere UTF-8 pode ficar dividido entre dois pedaços.
    const chunks: Buffer[] = [];
    for await (const chunk of req) chunks.push(chunk as Buffer);
    const body = Buffer.concat(chunks).toString('utf8');
    const path = (req.url ?? '/').split('?')[0];
    const r: Requisicao = { method: req.method ?? 'GET', path, headers: req.headers, body, url: req.url ?? '/' };
    requisicoes.push(r);
    const alvo = [...rotas].reverse().find(x => x.method === r.method
      && (typeof x.path === 'string' ? x.path === path : x.path.test(path)));
    const out = alvo ? await alvo.rota(r) : { status: 404, json: { title: 'Rota não configurada no fake.' } };
    res.statusCode = out.status ?? 200;
    if (out.json !== undefined) { res.setHeader('content-type', 'application/json'); res.end(JSON.stringify(out.json)); }
    else res.end(out.text ?? '');
  });
  await new Promise<void>(ok => server.listen(0, '127.0.0.1', ok));
  const { port } = server.address() as AddressInfo;
  return {
    url: `http://127.0.0.1:${port}`,
    requisicoes,
    on(method, path, rota) { rotas.push({ method, path, rota: typeof rota === 'function' ? rota : () => rota }); },
    escritas: () => requisicoes.filter(r => r.method !== 'GET' && r.method !== 'HEAD'),
    close: () => new Promise<void>(ok => server.close(() => ok())),
  };
}
