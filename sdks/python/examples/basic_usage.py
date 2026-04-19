"""Basic usage example for the Nexus Python SDK."""
import base64
from nexus_sdk import NexusClient

client = NexusClient(base_url="http://localhost:5000", tenant_id="tenant-default")

# Health check
health = client.health()
print("Health:", health)

# List agents
agents = client.list_agents(page=1, page_size=10)
print(f"Total agents: {agents.get('totalCount', 0)}")
for agent in agents.get("items", [])[:3]:
    print(f"  - {agent['id']}: {agent['name']} ({agent['capability']})")

# List swarms
swarms = client.list_swarms()
print(f"Total swarms: {swarms.get('totalCount', 0)}")

# Kyber key generation
kyber = client.kyber_keypair("Kyber768")
print(f"Kyber public key size: {len(base64.b64decode(kyber['publicKey']))} bytes")

# Run Shamir split
import base64
secret_b64 = base64.b64encode(b"SuperSecret42!").decode()
shares = client.shamir_split(secret_b64, threshold=3, total=5)
print(f"Shamir shares created: {shares.get('shareCount', 0)}")
