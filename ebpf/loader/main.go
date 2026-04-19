package main

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"os/signal"
	"syscall"
	"time"
)

// SyscallEvent mirrors the eBPF struct
type SyscallEvent struct {
	TimestampNs uint64 `json:"timestampNs"`
	Pid         uint32 `json:"pid"`
	Tgid        uint32 `json:"tgid"`
	SyscallID   uint64 `json:"syscallId"`
	Comm        string `json:"comm"`
}

// PacketStats mirrors the eBPF pkt_stats map
type PacketStats struct {
	TotalPackets uint64 `json:"totalPackets"`
	TotalBytes   uint64 `json:"totalBytes"`
	DroppedPkts  uint64 `json:"droppedPackets"`
}

// EBPFLoader simulates loading eBPF programs (requires Linux + root in production)
type EBPFLoader struct {
	packetMonitorPath string
	syscallTracerPath string
	running           bool
	stats             PacketStats
}

func NewEBPFLoader(packetMonitor, syscallTracer string) *EBPFLoader {
	return &EBPFLoader{
		packetMonitorPath: packetMonitor,
		syscallTracerPath: syscallTracer,
	}
}

// Load attempts to load eBPF programs. On non-Linux systems, runs in simulation mode.
func (l *EBPFLoader) Load() error {
	log.Printf("eBPF loader: attempting to load programs")
	log.Printf("  packet_monitor: %s", l.packetMonitorPath)
	log.Printf("  syscall_tracer: %s", l.syscallTracerPath)

	// On non-Linux or without root, run in simulation mode
	if os.Getenv("NEXUS_EBPF_SIMULATE") != "" || !isLinux() {
		log.Println("eBPF: running in SIMULATION mode (set NEXUS_EBPF_SIMULATE= to disable)")
		l.running = true
		return nil
	}

	// Production: use cilium/ebpf or libbpfgo here
	// For now, log that root + kernel headers are needed
	log.Println("eBPF: production mode requires: Linux kernel 5.15+, CAP_BPF, libbpf")
	l.running = true
	return nil
}

func (l *EBPFLoader) PollStats() PacketStats {
	if !l.running {
		return PacketStats{}
	}
	// Simulation: generate synthetic stats
	l.stats.TotalPackets += uint64(100 + time.Now().UnixNano()%50)
	l.stats.TotalBytes += l.stats.TotalPackets * 64
	return l.stats
}

func (l *EBPFLoader) Close() {
	l.running = false
	log.Println("eBPF loader: closed")
}

func isLinux() bool {
	return false // simplified; in prod: runtime.GOOS == "linux"
}

func main() {
	loader := NewEBPFLoader(
		"../src/packet_monitor.o",
		"../src/syscall_tracer.o",
	)

	if err := loader.Load(); err != nil {
		log.Fatalf("load: %v", err)
	}
	defer loader.Close()

	sigs := make(chan os.Signal, 1)
	signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM)

	ticker := time.NewTicker(5 * time.Second)
	defer ticker.Stop()

	fmt.Println("Nexus eBPF loader running. Press Ctrl+C to stop.")

	enc := json.NewEncoder(os.Stdout)
	enc.SetIndent("", "  ")

	for {
		select {
		case <-sigs:
			fmt.Println("\nShutting down...")
			return
		case <-ticker.C:
			stats := loader.PollStats()
			if err := enc.Encode(stats); err != nil {
				log.Printf("encode: %v", err)
			}
		}
	}
}
