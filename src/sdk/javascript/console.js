import readline from 'node:readline/promises';
import { stdin as input, stdout as output } from 'node:process';
import { XenoApiError, XenoClient } from './src/index.js';

const rl = readline.createInterface({ input, output });
let baseUrl = process.argv[2] || 'http://localhost:9000';
let tenantId = null;
let bearerToken = 'default';

function client() {
  return new XenoClient({ baseUrl, tenantId, bearerToken });
}

function printHelp() {
  console.log('');
  console.log('Connection');
  console.log(`  url <url>       set server URL, current ${baseUrl}`);
  console.log(`  tenant <id>     set tenant header, current ${tenantId ?? '<inferred>'}`);
  console.log(`  token <token>   set bearer token, current ${bearerToken ?? '<none>'}`);
  console.log('');
  console.log('Read commands');
  console.log('  health          server health');
  console.log('  ping            server heartbeat');
  console.log('  models          OpenAI-compatible model list');
  console.log('  clients         connected/registered clients');
  console.log('  tenants         tenants');
  console.log('  users           users');
  console.log('  credentials     credentials');
  console.log('  roles           roles');
  console.log('  permissions     permissions');
  console.log('  routing-policies routing policies');
  console.log('  history         request history');
  console.log('  usage           usage analytics');
  console.log('  leaderboard     public model token leaderboard');
  console.log('  ledger-status   ledger queue and NetLedger health');
  console.log('  ledger-balance  own ledger balance; append user ID for a specific user');
  console.log('  ledger-balances visible ledger balances');
  console.log('  ledger-entries  own ledger entries');
  console.log('  settings        server settings');
  console.log('  metrics         Prometheus metrics');
  console.log('');
  console.log('Actions');
  console.log('  login           exchange username/password for a client credential');
  console.log('  chat            send a chat completion request');
  console.log('  ledger-drain    drain queued ledger events');
  console.log('  ledger-reconcile report missing ledger links');
  console.log('');
  console.log('Other');
  console.log('  cls             clear screen');
  console.log('  q               quit');
  console.log('');
}

async function prompt(label, fallback) {
  const value = await rl.question(`${label} [${fallback}]: `);
  return value.trim() || fallback;
}

async function printJson(value) {
  console.log(JSON.stringify(value, null, 2));
}

async function runCommand(command) {
  const sdk = client();
  if (command === 'health') return printJson(await sdk.getHealth());
  if (command === 'ping') return printJson(await sdk.ping({ sequence: 1, clientSentUtc: new Date() }));
  if (command === 'models') return printJson(await sdk.listOpenAiModels());
  if (command === 'clients') return printJson(await sdk.listClients());
  if (command === 'tenants') return printJson(await sdk.listTenants());
  if (command === 'users') return printJson(await sdk.listUsers());
  if (command === 'credentials') return printJson(await sdk.listCredentials());
  if (command === 'roles') return printJson(await sdk.listRoles());
  if (command === 'permissions') return printJson(await sdk.listPermissions());
  if (command === 'routing-policies') return printJson(await sdk.listRoutingPolicies());
  if (command === 'history') return printJson(await sdk.listRequestHistory());
  if (command === 'usage') return printJson(await sdk.getUsageAnalytics());
  if (command === 'leaderboard') return printJson(await sdk.getModelTokenLeaderboard());
  if (command === 'ledger-status') return printJson(await sdk.getLedgerStatus());
  if (command === 'ledger-balance') return printJson(await sdk.getMyLedgerBalance());
  if (command.startsWith('ledger-balance ')) return printJson(await sdk.getLedgerUserBalance(command.slice(15).trim()));
  if (command === 'ledger-balances') return printJson(await sdk.searchLedgerBalances());
  if (command === 'ledger-entries') return printJson(await sdk.getMyLedgerEntries());
  if (command === 'ledger-drain') return printJson(await sdk.drainLedgerQueue());
  if (command === 'ledger-reconcile') return printJson(await sdk.reconcileLedger());
  if (command === 'settings') return printJson(await sdk.getSettings());
  if (command === 'metrics') return console.log(await sdk.getMetrics());
  if (command === 'login') {
    const username = await prompt('Username', 'admin@xeno');
    const password = await prompt('Password', 'password');
    return printJson(await sdk.login(username, password, tenantId ?? 'ten_default'));
  }
  if (command === 'chat') {
    const model = await prompt('Model', 'llama3.2:latest');
    const content = await prompt('Prompt', 'hello');
    return printJson(await sdk.createChatCompletion({ model, messages: [{ role: 'user', content }] }));
  }

  console.error('Unknown command. Type ? for help.');
}

printHelp();
let running = true;
while (running) {
  const line = (await rl.question('xeno-sdk [? for help]> ')).trim();
  try {
    if (!line) continue;
    if (line === '?' || line === 'help') printHelp();
    else if (line === 'q' || line === 'quit') running = false;
    else if (line === 'cls') console.clear();
    else if (line.startsWith('url ')) baseUrl = line.slice(4).trim();
    else if (line.startsWith('tenant ')) tenantId = line.slice(7).trim();
    else if (line.startsWith('token ')) bearerToken = line.slice(6).trim();
    else await runCommand(line);
  } catch (error) {
    if (error instanceof XenoApiError) {
      console.error(`API ${error.status} at ${error.endpoint}: ${error.message}`);
    } else {
      console.error(`Error: ${error.message}`);
    }
  }
}

rl.close();
