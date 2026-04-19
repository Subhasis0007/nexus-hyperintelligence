package main

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"time"
)

// AgentManifest describes a compiled WASM agent
type AgentManifest struct {
	ID         string            `json:"id"`
	Capability string            `json:"capability"`
	Version    uint32            `json:"version"`
	WasmPath   string            `json:"wasmPath"`
	SizeBytes  int64             `json:"sizeBytes"`
	LoadedAt   time.Time         `json:"loadedAt"`
	Metadata   map[string]string `json:"metadata"`
}

// WasmLoader discovers and loads compiled WASM agent modules
type WasmLoader struct {
	agentsDir string
	manifests []AgentManifest
}

func NewWasmLoader(agentsDir string) *WasmLoader {
	return &WasmLoader{agentsDir: agentsDir}
}

// Discover finds all .wasm files in the agents directory
func (l *WasmLoader) Discover() error {
	entries, err := os.ReadDir(l.agentsDir)
	if err != nil {
		return fmt.Errorf("reading agents dir %q: %w", l.agentsDir, err)
	}

	l.manifests = nil
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}
		if filepath.Ext(entry.Name()) != ".wasm" {
			continue
		}
		wasmPath := filepath.Join(l.agentsDir, entry.Name())
		info, err := entry.Info()
		if err != nil {
			log.Printf("WARN: stat %q: %v", wasmPath, err)
			continue
		}
		manifest := AgentManifest{
			ID:        entry.Name()[:len(entry.Name())-5], // strip .wasm
			WasmPath:  wasmPath,
			SizeBytes: info.Size(),
			LoadedAt:  time.Now().UTC(),
			Metadata:  make(map[string]string),
		}
		// In a real loader we'd call agent_capability() via a WASM runtime (e.g. wasmer-go / wasmtime-go)
		manifest.Capability = inferCapability(manifest.ID)
		manifest.Version = 1
		l.manifests = append(l.manifests, manifest)
	}
	return nil
}

// inferCapability maps agent ID to capability name (mirrors the Rust source)
func inferCapability(id string) string {
	caps := map[string]string{
		"agent_001": "Analytics",
		"agent_002": "Security",
		"agent_003": "DataIngestion",
		"agent_004": "Intelligence",
		"agent_005": "Prediction",
		"agent_006": "Compliance",
		"agent_007": "Monitoring",
		"agent_008": "Orchestration",
		"agent_009": "Knowledge",
		"agent_010": "Communication",
	}
	if cap, ok := caps[id]; ok {
		return cap
	}
	return "Unknown"
}

// Manifests returns all discovered manifests
func (l *WasmLoader) Manifests() []AgentManifest {
	return l.manifests
}

func main() {
	agentsDir := "dist"
	if len(os.Args) > 1 {
		agentsDir = os.Args[1]
	}

	loader := NewWasmLoader(agentsDir)

	// If dist/ doesn't exist yet (pre-build), create a stub listing
	if err := os.MkdirAll(agentsDir, 0o755); err != nil {
		log.Fatalf("creating dir: %v", err)
	}

	if err := loader.Discover(); err != nil {
		log.Fatalf("discover: %v", err)
	}

	manifests := loader.Manifests()
	if len(manifests) == 0 {
		fmt.Printf("No .wasm files found in %q\n", agentsDir)
		fmt.Println("Build agents first: cd wasm-agents && cargo build --release --target wasm32-unknown-unknown")
		fmt.Println("Then copy *.wasm files to dist/")
		os.Exit(0)
	}

	enc := json.NewEncoder(os.Stdout)
	enc.SetIndent("", "  ")
	if err := enc.Encode(manifests); err != nil {
		log.Fatalf("encoding manifests: %v", err)
	}
}
