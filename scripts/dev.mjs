// Ambiente local isolado. Não acessa banco ou configuração de produção.
import { randomBytes } from 'node:crypto';
import { existsSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = fileURLToPath(new URL('../', import.meta.url));
const envFile = fileURLToPath(new URL('../.env.dev', import.meta.url));
const stop = process.argv.includes('--stop');
function run(args) {
  const result = spawnSync('docker', args, { cwd: root, stdio: 'inherit' });
  if (result.error) {
    console.error('Docker não encontrado. Instale e abra o Docker Desktop (containers Linux).');
    process.exit(1);
  }
  if (result.status !== 0) process.exit(result.status ?? 1);
}
run(['compose', 'version']);
if (!existsSync(envFile)) {
  if (stop) { console.log('Ambiente local ainda não configurado.'); process.exit(0); }
  writeFileSync(envFile, [
    `MSSQL_SA_PASSWORD=Pr1!${randomBytes(24).toString('hex')}`,
    `PRISMA_JWT_KEY=${randomBytes(48).toString('hex')}`,
    `PRISMA_SETUP_TOKEN=${randomBytes(32).toString('base64url')}`,
    '',
  ].join('\n'), { flag: 'wx', mode: 0o600 });
}
const compose = ['compose', '--env-file', '.env.dev', '-f', 'compose.dev.yaml'];
if (stop) {
  run([...compose, 'stop']);
} else {
  console.log('SQL Server Developer: ambiente exclusivo de desenvolvimento e testes.');
  console.log('Ao iniciar, você aceita a EULA do SQL Server indicada no guia local.');
  console.log('Frontend: http://localhost:5450 | API: http://localhost:5400');
  console.log('Primeiro acesso: /setup. Consulte PRISMA_SETUP_TOKEN em .env.dev.');
  console.log('As URLs ficam disponíveis após o restore e a compilação inicial.');
  console.log('Ctrl+C encerra os serviços; os dados locais são preservados.');
  run([...compose, 'up', '--remove-orphans']);
}
