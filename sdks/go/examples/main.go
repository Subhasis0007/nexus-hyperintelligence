package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"

	nexus "github.com/nexus-hyperintelligence/sdk/nexus"
)

func main() {
	client := nexus.NewClient("http://localhost:5000", "tenant-default")
	ctx := context.Background()

	health, err := client.Health(ctx)
	if err != nil {
		log.Fatalf("health: %v", err)
	}
	fmt.Println("Health:", string(health))

	agents, err := client.ListAgents(ctx, 1, 5)
	if err != nil {
		log.Fatalf("list agents: %v", err)
	}
	var result map[string]json.RawMessage
	_ = json.Unmarshal(agents, &result)
	fmt.Println("Agents:", string(result["totalCount"]))

	kyber, err := client.KyberKeypair(ctx, "Kyber768")
	if err != nil {
		log.Fatalf("kyber: %v", err)
	}
	fmt.Println("Kyber keypair:", len(kyber), "bytes JSON")
}
