// SPDX-License-Identifier: GPL-2.0
// Nexus eBPF packet monitor — attaches to XDP and counts packets per protocol
#include <linux/bpf.h>
#include <linux/if_ether.h>
#include <linux/ip.h>
#include <linux/ipv6.h>
#include <linux/tcp.h>
#include <linux/udp.h>
#include <bpf/bpf_helpers.h>
#include <bpf/bpf_endian.h>

// ── Maps ──────────────────────────────────────────────────────────────────────
struct {
    __uint(type, BPF_MAP_TYPE_PERCPU_ARRAY);
    __uint(max_entries, 256);  // indexed by IP protocol number
    __type(key, __u32);
    __type(value, __u64);
} proto_counter SEC(".maps");

struct {
    __uint(type, BPF_MAP_TYPE_PERCPU_ARRAY);
    __uint(max_entries, 3);    // 0=total, 1=bytes, 2=dropped
    __type(key, __u32);
    __type(value, __u64);
} pkt_stats SEC(".maps");

struct {
    __uint(type, BPF_MAP_TYPE_LRU_HASH);
    __uint(max_entries, 10240);
    __type(key, __u32);   // source IP
    __type(value, __u64); // packet count from this source
} src_ip_counter SEC(".maps");

// ── XDP program ───────────────────────────────────────────────────────────────
SEC("xdp")
int nexus_packet_monitor(struct xdp_md *ctx)
{
    void *data_end = (void *)(long)ctx->data_end;
    void *data     = (void *)(long)ctx->data;

    // ── Total packet counter ──
    __u32 stats_key = 0;
    __u64 *total = bpf_map_lookup_elem(&pkt_stats, &stats_key);
    if (total) __sync_fetch_and_add(total, 1);

    // ── Byte counter ──
    __u32 bytes_key = 1;
    __u64 *bytes = bpf_map_lookup_elem(&pkt_stats, &bytes_key);
    if (bytes) {
        __u32 pkt_len = (__u32)(data_end - data);
        __sync_fetch_and_add(bytes, pkt_len);
    }

    // ── Parse Ethernet ──
    struct ethhdr *eth = data;
    if ((void *)(eth + 1) > data_end)
        return XDP_PASS;

    __u16 eth_proto = bpf_ntohs(eth->h_proto);

    if (eth_proto == ETH_P_IP) {
        struct iphdr *ip = (void *)(eth + 1);
        if ((void *)(ip + 1) > data_end)
            return XDP_PASS;

        // ── Protocol counter ──
        __u32 proto = ip->protocol;
        __u64 *cnt = bpf_map_lookup_elem(&proto_counter, &proto);
        if (cnt) __sync_fetch_and_add(cnt, 1);

        // ── Source IP counter ──
        __u32 src_ip = ip->saddr;
        __u64 *ip_cnt = bpf_map_lookup_elem(&src_ip_counter, &src_ip);
        if (ip_cnt) {
            __sync_fetch_and_add(ip_cnt, 1);
        } else {
            __u64 one = 1;
            bpf_map_update_elem(&src_ip_counter, &src_ip, &one, BPF_ANY);
        }
    }

    return XDP_PASS;
}

char _license[] SEC("license") = "GPL";
