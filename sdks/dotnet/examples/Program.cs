using Nexus.SDK;

using var client = new NexusClient("http://localhost:5000", "tenant-default");

var health = await client.HealthAsync();
Console.WriteLine($"Health: {health}");

var agents = await client.ListAgentsAsync(page: 1, pageSize: 5);
Console.WriteLine($"Agents response: {agents}");

var kyber = await client.KyberKeypairAsync("Kyber768");
Console.WriteLine($"Kyber keypair: {kyber}");
