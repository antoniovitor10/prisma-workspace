// Autenticação e aliases da versão oficial fixada; nunca executa geração.
import path from 'node:path';
import fs from 'node:fs/promises';
process.umask(0o077);
const mode = process.argv[2];
if (!['login', 'status', 'models'].includes(mode)) process.exit(2);
const core = await import('/usr/local/lib/node_modules/@google/gemini-cli-core/dist/index.js');
const reply = value => process.stdout.write(`PRISMA_GEMINI_AUTH=${JSON.stringify(value)}\n`);

if (mode === 'models') {
  const names = new Map([[core.GEMINI_MODEL_ALIAS_AUTO, 'Automático (alias da CLI)'], [core.GEMINI_MODEL_ALIAS_PRO, 'Pro (alias da CLI)'], [core.GEMINI_MODEL_ALIAS_FLASH, 'Flash (alias da CLI)'], [core.GEMINI_MODEL_ALIAS_FLASH_LITE, 'Flash Lite (alias da CLI)']]);
  const ids = [...new Set([...names.keys(), ...core.VALID_GEMINI_MODELS])].filter(id => typeof id === 'string' && (names.has(id) || id.startsWith('gemini-')));
  reply({ models: ids.map(id => ({ id, name: names.get(id) ?? id })), state: 'ready', source: 'gemini', message: 'Modelos e aliases da Gemini CLI instalada. O teste confirma o acesso da sua conta.' });
} else {
  // Login usa um diretório temporário; status usa somente o volume privado.
  const config = { getProxy: () => undefined, isBrowserLaunchSuppressed: () => true, isInteractive: () => mode === 'login', getAcpMode: () => false };
  try {
    const client = await core.getOauthClient(core.AuthType.LOGIN_WITH_GOOGLE, config);
    const { token } = await client.getAccessToken();
    if (!token) throw new Error('Authentication required');
    await client.getTokenInfo(token);
    // A troca e o cache são da CLI oficial; não há tokens na resposta.
    const cache = core.Storage.getOAuthCredsPath();
    const stat = await fs.stat(cache);
    if (!stat.isFile() || stat.size > 32000) throw new Error('Invalid cache');
    await fs.chmod(cache, 0o600);
    if (mode === 'login' && path.resolve(cache) !== path.resolve(process.cwd(), '.gemini', 'oauth_creds.json')) throw new Error('Invalid login destination');
    reply({ authenticated: true });
  } catch {
    reply({ authenticated: false });
    if (mode === 'login') process.exitCode = 1;
  }
}
