import { expect, it } from 'vitest';
import { selectSeedProject } from '../../e2e/fixtures/seedProject';
const demo = { key: 'DEMO', id: 'canonical' };
const legacy = { key: 'INFRACOES', id: 'legacy' };
const generated = { key: 'E2E-L08', id: 'isolated' };
it('prefere DEMO mesmo com fixture anterior na lista', () => {
  expect(selectSeedProject([generated, demo])).toBe(demo);
});
it('mantém a escolha quando a ordenação dos projetos muda', () => {
  expect(selectSeedProject([demo, generated])).toBe(demo);
});
it('prefere o seed Community atual quando o legado também existe', () => {
  expect(selectSeedProject([legacy, demo])).toBe(demo);
});
it('aceita explicitamente o seed legado', () => {
  expect(selectSeedProject([legacy, generated])).toBe(legacy);
});
it('falha antes de navegar quando só existem projetos de outras fixtures', () => {
  expect(() => selectSeedProject([generated])).toThrow('Projeto seed');
});
