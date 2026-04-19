import * as vscode from 'vscode';
import * as https from 'https';
import * as http from 'http';
import { URL } from 'url';

// ── Types ─────────────────────────────────────────────────────────────────────

interface AgentItem {
  id: string;
  name: string;
  capability: string;
  status: string;
}

interface SwarmItem {
  id: string;
  name: string;
  type: string;
  agentCount: number;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
}

// ── HTTP helper ───────────────────────────────────────────────────────────────

function nexusGet<T>(endpoint: string, tenantId: string, path: string): Promise<T> {
  return new Promise((resolve, reject) => {
    const base = new URL(endpoint);
    const options: http.RequestOptions = {
      hostname: base.hostname,
      port: base.port || (base.protocol === 'https:' ? 443 : 80),
      path,
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'X-Tenant-ID': tenantId,
      },
    };
    const proto = base.protocol === 'https:' ? https : http;
    const req = proto.request(options, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (c: Buffer) => chunks.push(c));
      res.on('end', () => {
        const raw = Buffer.concat(chunks).toString('utf8');
        try {
          resolve(JSON.parse(raw) as T);
        } catch {
          reject(new Error(`JSON parse error: ${raw.slice(0, 200)}`));
        }
      });
    });
    req.on('error', reject);
    req.setTimeout(10_000, () => { req.destroy(); reject(new Error('Request timed out')); });
    req.end();
  });
}

// ── Tree providers ────────────────────────────────────────────────────────────

class AgentTreeItem extends vscode.TreeItem {
  constructor(agent: AgentItem) {
    super(agent.name, vscode.TreeItemCollapsibleState.None);
    this.description = `${agent.capability} · ${agent.status}`;
    this.tooltip = `ID: ${agent.id}\nCapability: ${agent.capability}\nStatus: ${agent.status}`;
    this.contextValue = 'nexusAgent';
    this.iconPath = new vscode.ThemeIcon(
      agent.status === 'Active' ? 'circle-filled' : 'circle-outline'
    );
  }
}

class AgentExplorerProvider implements vscode.TreeDataProvider<AgentTreeItem> {
  private _onDidChangeTreeData = new vscode.EventEmitter<AgentTreeItem | undefined | null | void>();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  constructor(private readonly context: vscode.ExtensionContext) {}

  refresh(): void { this._onDidChangeTreeData.fire(); }

  getTreeItem(element: AgentTreeItem): vscode.TreeItem { return element; }

  async getChildren(): Promise<AgentTreeItem[]> {
    const config = vscode.workspace.getConfiguration('nexus');
    const endpoint = config.get<string>('apiEndpoint', 'http://localhost:5000');
    const tenantId = config.get<string>('tenantId', 'tenant-default');
    try {
      const result = await nexusGet<PagedResult<AgentItem>>(
        endpoint, tenantId, '/api/agents?page=1&pageSize=200'
      );
      return (result.items ?? []).map(a => new AgentTreeItem(a));
    } catch (err) {
      vscode.window.showWarningMessage(`Nexus Agents: ${(err as Error).message}`);
      return [];
    }
  }
}

class SwarmTreeItem extends vscode.TreeItem {
  constructor(swarm: SwarmItem) {
    super(swarm.name, vscode.TreeItemCollapsibleState.None);
    this.description = `${swarm.type} · ${swarm.agentCount} agents`;
    this.tooltip = `ID: ${swarm.id}\nType: ${swarm.type}\nAgents: ${swarm.agentCount}`;
    this.contextValue = 'nexusSwarm';
    this.iconPath = new vscode.ThemeIcon('server-environment');
  }
}

class SwarmExplorerProvider implements vscode.TreeDataProvider<SwarmTreeItem> {
  private _onDidChangeTreeData = new vscode.EventEmitter<SwarmTreeItem | undefined | null | void>();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  constructor(private readonly context: vscode.ExtensionContext) {}

  refresh(): void { this._onDidChangeTreeData.fire(); }

  getTreeItem(element: SwarmTreeItem): vscode.TreeItem { return element; }

  async getChildren(): Promise<SwarmTreeItem[]> {
    const config = vscode.workspace.getConfiguration('nexus');
    const endpoint = config.get<string>('apiEndpoint', 'http://localhost:5000');
    const tenantId = config.get<string>('tenantId', 'tenant-default');
    try {
      const result = await nexusGet<PagedResult<SwarmItem>>(endpoint, tenantId, '/api/swarms');
      return (result.items ?? []).map(s => new SwarmTreeItem(s));
    } catch (err) {
      vscode.window.showWarningMessage(`Nexus Swarms: ${(err as Error).message}`);
      return [];
    }
  }
}

// ── Extension lifecycle ───────────────────────────────────────────────────────

export function activate(context: vscode.ExtensionContext): void {
  const agentProvider = new AgentExplorerProvider(context);
  const swarmProvider = new SwarmExplorerProvider(context);

  context.subscriptions.push(
    vscode.window.registerTreeDataProvider('nexusAgentExplorer', agentProvider),
    vscode.window.registerTreeDataProvider('nexusSwarmExplorer', swarmProvider),

    vscode.commands.registerCommand('nexus.refreshAgents', () => agentProvider.refresh()),
    vscode.commands.registerCommand('nexus.refreshSwarms', () => swarmProvider.refresh()),

    vscode.commands.registerCommand('nexus.setEndpoint', async () => {
      const config = vscode.workspace.getConfiguration('nexus');
      const current = config.get<string>('apiEndpoint', 'http://localhost:5000');
      const value = await vscode.window.showInputBox({
        prompt: 'Nexus API endpoint URL',
        value: current,
        validateInput: (v) => {
          try { new URL(v); return null; } catch { return 'Enter a valid URL'; }
        },
      });
      if (value) {
        await config.update('apiEndpoint', value, vscode.ConfigurationTarget.Global);
        agentProvider.refresh();
        swarmProvider.refresh();
      }
    }),

    vscode.commands.registerCommand('nexus.runHealthCheck', async () => {
      const config = vscode.workspace.getConfiguration('nexus');
      const endpoint = config.get<string>('apiEndpoint', 'http://localhost:5000');
      const tenantId = config.get<string>('tenantId', 'tenant-default');
      try {
        const result = await nexusGet<Record<string, unknown>>(endpoint, tenantId, '/health');
        vscode.window.showInformationMessage(`Nexus Health: ${JSON.stringify(result)}`);
      } catch (err) {
        vscode.window.showErrorMessage(`Nexus Health Check failed: ${(err as Error).message}`);
      }
    }),

    vscode.commands.registerCommand('nexus.generateKyberKeypair', async () => {
      const config = vscode.workspace.getConfiguration('nexus');
      const endpoint = config.get<string>('apiEndpoint', 'http://localhost:5000');
      const tenantId = config.get<string>('tenantId', 'tenant-default');
      try {
        const result = await nexusGet<Record<string, string>>(
          endpoint, tenantId, '/api/crypto/kyber/keypair?level=Kyber768'
        );
        const doc = await vscode.workspace.openTextDocument({
          content: JSON.stringify(result, null, 2),
          language: 'json',
        });
        await vscode.window.showTextDocument(doc);
      } catch (err) {
        vscode.window.showErrorMessage(`Kyber keypair generation failed: ${(err as Error).message}`);
      }
    }),
  );

  vscode.window.showInformationMessage('Nexus HyperIntelligence extension activated.');
}

export function deactivate(): void {}
