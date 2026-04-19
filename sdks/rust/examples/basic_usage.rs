use nexus_sdk::NexusClient;

fn main() {
    let client = NexusClient::new("localhost", 5000, "tenant-default");

    match client.health() {
        Ok(h) => println!("Health: {}", h),
        Err(e) => eprintln!("Health check failed (API may not be running): {e}"),
    }

    match client.list_agents(1, 5) {
        Ok(agents) => println!("Agents: {}", agents["totalCount"]),
        Err(e) => eprintln!("List agents failed: {e}"),
    }

    match client.kyber_keypair("Kyber768") {
        Ok(k) => println!("Kyber public key: {} bytes (base64)", k["publicKey"].as_str().unwrap_or("").len()),
        Err(e) => eprintln!("Kyber failed: {e}"),
    }
}
