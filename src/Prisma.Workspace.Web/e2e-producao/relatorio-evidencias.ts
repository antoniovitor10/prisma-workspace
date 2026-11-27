import { copyFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FullResult, Reporter, TestCase, TestResult } from '@playwright/test/reporter';

/**
 * Leva os prints e um resumo para a pasta de evidências (CA-9), verde ou não.
 *
 * A pasta vem de `PROD_E2E_EVIDENCIAS` (o agente passa
 * `docs/trabalho/<HU>/evidencias/<T-XXXX>/` do espaço de trabalho, fora do repo).
 * Sem a variável, vai para `e2e-producao/resultados/evidencias/`, ignorada pelo git.
 * Dentro dela, cada execução ganha uma subpasta com o carimbo de início.
 * Nenhum texto do resumo traz e-mail ou senha: os valores do arquivo de credenciais
 * são trocados por `***` antes de gravar, por garantia.
 */
type Linha = { tela: string; titulo: string; status: string; duracaoMs: number; erro?: string };

/** Códigos de cor ANSI das mensagens de erro do Playwright. */
const CODIGOS_DE_COR = new RegExp(`${String.fromCharCode(27)}\\[[0-9;]*m`, 'g');
const raizWeb = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function ocultarSegredos(texto: string): string {
  let limpo = texto;
  for (const nome of ['PROD_E2E_PASSWORD', 'PROD_E2E_EMAIL']) {
    const valor = (process.env[nome] ?? '').trim();
    if (valor) limpo = limpo.split(valor).join('***');
  }
  return limpo;
}

function seguro(nome: string): string {
  return nome.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '');
}

export default class RelatorioEvidencias implements Reporter {
  private readonly linhas: Linha[] = [];
  private readonly prints: string[] = [];
  private readonly inicio = new Date();
  private sobras: string | undefined;
  private pasta = '';

  onBegin(): void {
    const configurada = (process.env.PROD_E2E_EVIDENCIAS ?? '').trim();
    const carimbo = this.inicio.toISOString().replace(/[:.]/g, '-');
    // Sempre uma subpasta por execução: uma rodada nunca sobrescreve as evidências de outra.
    this.pasta = join(configurada ? resolve(configurada) : join(raizWeb, 'e2e-producao', 'resultados', 'evidencias'), carimbo);
  }

  onTestEnd(test: TestCase, result: TestResult): void {
    const tela = test.parent.project()?.name ?? '';
    const anotacoes = [...test.annotations, ...(result.annotations ?? [])];
    const sobras = anotacoes.find(anotacao => anotacao.type === 'sobras-arquivadas');
    if (sobras?.description !== undefined) this.sobras = sobras.description;

    const erro = result.errors.map(item => (item.message ?? '').replace(CODIGOS_DE_COR, '')).join('\n').split('\n').slice(0, 6).join(' ').trim();
    this.linhas.push({ tela, titulo: test.title, status: result.status, duracaoMs: result.duration, ...(erro ? { erro } : {}) });

    mkdirSync(this.pasta, { recursive: true });
    for (const anexo of result.attachments) {
      if (anexo.contentType !== 'image/png' || !anexo.path || !existsSync(anexo.path)) continue;
      const nome = `${String(this.prints.length + 1).padStart(2, '0')}-${seguro(tela)}-${seguro(anexo.name === 'screenshot' ? `falha-${basename(anexo.path, '.png')}` : anexo.name)}.png`;
      copyFileSync(anexo.path, join(this.pasta, nome));
      this.prints.push(nome);
    }
  }

  onEnd(resultado: FullResult): void {
    if (this.linhas.length === 0) return; // --list ou nada executado: não grava resumo.
    mkdirSync(this.pasta, { recursive: true });
    const tarefa = (process.env.PROD_E2E_TASK ?? '').trim();
    const fim = new Date();
    const texto = [
      '# Suíte Playwright de produção: resumo',
      '',
      `- Resultado geral: **${resultado.status}**`,
      `- Início: ${this.inicio.toISOString()} · fim: ${fim.toISOString()}`,
      `- Endereço: ${(process.env.PROD_E2E_BASE_URL ?? '').trim() || 'https://prisma.nordevs.com.br'}`,
      `- Spec da task: ${tarefa ? `tasks/${tarefa}.spec.ts` : 'nenhum (só a base)'}`,
      `- Sobras [e2e-prod] arquivadas no início: ${this.sobras ?? 'não informado (a preparação não terminou)'}`,
      '',
      '| Tela | Teste | Resultado | Duração |',
      '|---|---|---|---|',
      ...this.linhas.map(linha => `| ${linha.tela} | ${linha.titulo} | ${linha.status} | ${(linha.duracaoMs / 1000).toFixed(1)} s |`),
      '',
      ...(this.linhas.some(linha => linha.erro)
        ? ['## Erros', '', ...this.linhas.filter(linha => linha.erro).map(linha => `- **${linha.tela} · ${linha.titulo}:** ${linha.erro}`), '']
        : []),
      '## Prints',
      '',
      ...(this.prints.length ? this.prints.map(print => `- ${print}`) : ['- nenhum']),
      '',
      'Relatório HTML (pasta ignorada pelo git): src/Prisma.Workspace.Web/e2e-producao/playwright-report/',
      '',
    ].join('\n');
    writeFileSync(join(this.pasta, 'resumo.md'), ocultarSegredos(texto));
    console.log(`[e2e-prod] Evidências em ${this.pasta}`);
  }

  printsToStdio(): boolean {
    return false;
  }
}
