import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve } from 'node:path';
import type { FullConfig, FullResult, Reporter, TestCase, TestResult, TestStep } from '@playwright/test/reporter';

type Location = { file: string; line: number; column: number };

/** Registra somente origem e duração; títulos, mensagens, argumentos e anexos podem conter segredos. */
export default class DiagnosticReporter implements Reporter {
  private root = '';
  private output: string;
  private sequence = 0;
  private steps = new WeakMap<TestStep, number>();

  constructor(options: { outputFile?: string } = {}) {
    this.output = resolve(options.outputFile ?? '../../.runlogs/oficina/e2e-diagnostic-steps.jsonl');
  }
  onBegin(config: FullConfig) {
    this.root = resolve(config.rootDir);
    mkdirSync(dirname(this.output), { recursive: true });
    writeFileSync(this.output, '');
    this.sequence = 0;
    this.steps = new WeakMap();
  }
  private location(location?: Location) {
    if (!location) return null;
    const path = relative(this.root, location.file);
    // Origens externas e caminhos arbitrários não são copiados para o relatório.
    if (isAbsolute(path) || path === '..' || path.startsWith('../') || path.startsWith('..\\')) return null;
    return { file: path.replaceAll('\\', '/'), line: location.line, column: location.column };
  }
  private emit(record: Record<string, unknown>) {
    appendFileSync(this.output, JSON.stringify(record) + '\n');
  }
  private context(test: TestCase, result: TestResult) {
    const name = test.parent.project()?.name;
    const project = ['setup', 'chromium-desktop', 'chromium-mobile'].includes(name ?? '') ? name : 'other';
    return { test: this.location(test.location), project, retry: result.retry, worker: result.workerIndex };
  }
  onStepBegin(test: TestCase, result: TestResult, step: TestStep) {
    const id = ++this.sequence;
    this.steps.set(step, id);
    const category = ['expect', 'fixture', 'hook', 'pw:api', 'test.step', 'test.attach'].includes(step.category)
      ? step.category : 'other';
    this.emit({ event: 'step_begin', ...this.context(test, result), id,
      category, location: this.location(step.location) });
  }
  onStepEnd(test: TestCase, result: TestResult, step: TestStep) {
    this.emit({ event: 'step_end', ...this.context(test, result), id: this.steps.get(step),
      durationMs: step.duration, failed: !!step.error });
  }
  onTestEnd(test: TestCase, result: TestResult) {
    this.emit({ event: 'test_end', ...this.context(test, result),
      status: result.status, durationMs: result.duration, errors: result.errors.length });
  }
  onEnd(result: FullResult) {
    this.emit({ event: 'run_end', status: result.status, durationMs: result.duration });
  }
  printsToStdio() { return false; }
}
