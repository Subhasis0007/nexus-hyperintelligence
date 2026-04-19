import { NexusClient } from '../src/index.js';

const client = new NexusClient({ baseUrl: 'http://localhost:5000', tenantId: 'tenant-default' });

try {
  const health = await client.health();
  console.log('Health:', JSON.stringify(health, null, 2));

  const agents = await client.listAgents(1, 5);
  console.log(`Total agents: ${agents.totalCount}`);
  for (const a of agents.items.slice(0, 3)) {
    console.log(`  - ${a.id}: ${a.name} (${a.capability})`);
  }

  const swarms = await client.listSwarms();
  console.log(`Total swarms: ${swarms.totalCount}`);

  const kyber = await client.kyberKeypair();
  const pubKeyBytes = Buffer.from(kyber.publicKey, 'base64');
  console.log(`Kyber public key: ${pubKeyBytes.length} bytes`);
} catch (err) {
  console.error('Error:', err.message);
}
