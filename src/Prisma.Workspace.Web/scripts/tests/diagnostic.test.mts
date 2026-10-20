// @vitest-environment node
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmdirSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'vitest';
import type { FullConfig, FullResult, TestCase, TestResult, TestStep } from '@playwright/test/reporter';
import DiagnosticReporter from '../../e2e/reporters/diagnostic.ts';

test('diagnóstico preserva origem/duração sem copiar conteúdo sensível', () => {
  const temporaryRoot = join(process.cwd(), '.runlogs', 'oficina');
  mkdirSync(temporaryRoot, { recursive: true });
  const root = mkdtempSync(join(temporaryRoot, 'reporter-'));
  try {
    const outputFile = join(root, 'result.jsonl');
    const reporter = new DiagnosticReporter({ outputFile });
    reporter.onBegin({ rootDir: root } as FullConfig);
    const secret = 'SECRET_SENTINEL_TOKEN_BODY_TITLE';
    const location = { file: join(root, 'timeout.spec.ts'), line: 12, column: 5 };
    const subject = { title: secret, id: secret, location, parent: { project: () => ({ name: 'chromium-mobile' }) } } as unknown as TestCase;
    const result = { retry: 0, workerIndex: 0, status: 'timedOut', duration: 30000,
      errors: [{ message: secret, stack: secret }], stdout: [secret], attachments: [{ body: secret }] } as unknown as TestResult;
    const step = { title: secret, category: 'pw:api', location, duration: 29000,
      error: { message: secret }, attachments: [{ body: secret }] } as unknown as TestStep;
    reporter.onStepBegin(subject, result, step);
    reporter.onStepEnd(subject, result, step);
    reporter.onTestEnd(subject, result);
    // Etapa externa com categoria arbitrária não pode injetar texto sensível.
    reporter.onStepBegin(subject, result, { ...step, category: secret,
      location: { ...location, file: join(root, '..', secret) } } as TestStep);
    reporter.onEnd({ status: 'failed', duration: 30001 } as FullResult);
    const raw = readFileSync(outputFile, 'utf8');
    assert.equal(raw.includes(secret), false);
    assert.equal(raw.includes(root), false);
    const records = raw.trim().split('\n').map(line => JSON.parse(line));
    assert.equal(records.length, 5);
    assert.deepEqual(records[0].location, { file: 'timeout.spec.ts', line: 12, column: 5 });
    assert.equal(records[0].id, records[1].id);
    assert.equal(records[1].durationMs, 29000);
    assert.equal(records[1].failed, true);
    assert.equal(records[2].status, 'timedOut');
    assert.equal(records[2].errors, 1);
    assert.equal(records[3].location, null);
    assert.equal(records[3].category, 'other');
    assert.equal(records[4].status, 'failed');
  } finally { unlinkSync(join(root, 'result.jsonl')); rmdirSync(root); }
});
