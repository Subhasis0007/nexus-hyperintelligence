// SPDX-License-Identifier: GPL-2.0
// Nexus eBPF syscall tracer — traces agent-relevant syscalls using tracepoints
#include <linux/bpf.h>
#include <linux/ptrace.h>
#include <bpf/bpf_helpers.h>
#include <bpf/bpf_tracing.h>

#define TASK_COMM_LEN 16
#define NEXUS_PREFIX  "nexus"

// ── Event structure sent to userspace via perf ring buffer ────────────────────
struct syscall_event {
    __u64 timestamp_ns;
    __u32 pid;
    __u32 tgid;
    __u64 syscall_id;
    char  comm[TASK_COMM_LEN];
};

// ── Maps ──────────────────────────────────────────────────────────────────────
struct {
    __uint(type, BPF_MAP_TYPE_PERF_EVENT_ARRAY);
    __uint(key_size, sizeof(__u32));
    __uint(value_size, sizeof(__u32));
} events SEC(".maps");

struct {
    __uint(type, BPF_MAP_TYPE_PERCPU_ARRAY);
    __uint(max_entries, 512);
    __type(key, __u32);
    __type(value, __u64);
} syscall_counts SEC(".maps");

// ── Tracepoint: sys_enter_openat ──────────────────────────────────────────────
SEC("tracepoint/syscalls/sys_enter_openat")
int trace_openat(struct trace_event_raw_sys_enter *ctx)
{
    __u64 id = bpf_get_current_pid_tgid();
    __u32 pid = id >> 32;

    struct syscall_event ev = {};
    ev.timestamp_ns = bpf_ktime_get_ns();
    ev.pid          = pid;
    ev.tgid         = (__u32)id;
    ev.syscall_id   = ctx->id;
    bpf_get_current_comm(&ev.comm, sizeof(ev.comm));

    bpf_perf_event_output(ctx, &events, BPF_F_CURRENT_CPU, &ev, sizeof(ev));

    __u32 sys_key = (__u32)(ctx->id % 512);
    __u64 *cnt = bpf_map_lookup_elem(&syscall_counts, &sys_key);
    if (cnt) __sync_fetch_and_add(cnt, 1);

    return 0;
}

// ── Tracepoint: sys_enter_write ───────────────────────────────────────────────
SEC("tracepoint/syscalls/sys_enter_write")
int trace_write(struct trace_event_raw_sys_enter *ctx)
{
    __u32 sys_key = 1;  // write = syscall 1 on x86-64
    __u64 *cnt = bpf_map_lookup_elem(&syscall_counts, &sys_key);
    if (cnt) __sync_fetch_and_add(cnt, 1);
    return 0;
}

// ── Tracepoint: sys_enter_connect ─────────────────────────────────────────────
SEC("tracepoint/syscalls/sys_enter_connect")
int trace_connect(struct trace_event_raw_sys_enter *ctx)
{
    __u64 id = bpf_get_current_pid_tgid();
    struct syscall_event ev = {};
    ev.timestamp_ns = bpf_ktime_get_ns();
    ev.pid          = (__u32)(id >> 32);
    ev.tgid         = (__u32)id;
    ev.syscall_id   = 42;  // connect
    bpf_get_current_comm(&ev.comm, sizeof(ev.comm));
    bpf_perf_event_output(ctx, &events, BPF_F_CURRENT_CPU, &ev, sizeof(ev));
    return 0;
}

char _license[] SEC("license") = "GPL";
