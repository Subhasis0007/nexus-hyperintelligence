---- MODULE NexusConsensus ----
\* TLA+ specification for the Nexus Raft-based consensus engine
\* Models leader election and log replication across a swarm of agents

EXTENDS Integers, FiniteSets, Sequences, TLC

CONSTANTS
    Agents,         \* Set of agent IDs in the swarm, e.g. {"a1","a2","a3","a4","a5"}
    MaxTerm,        \* Maximum election term to bound state space
    MaxLog          \* Maximum log entries per agent

VARIABLES
    role,           \* role[a] ∈ {Follower, Candidate, Leader}
    currentTerm,    \* currentTerm[a] ∈ Nat
    votedFor,       \* votedFor[a] ∈ Agents ∪ {None}
    log,            \* log[a] = sequence of log entries
    commitIndex,    \* commitIndex[a] ∈ Nat
    votesGranted    \* votesGranted[a] ⊆ Agents

\* ── Type invariants ───────────────────────────────────────────────────────────
TypeInvariant ==
    /\ role ∈ [Agents -> {"Follower", "Candidate", "Leader"}]
    /\ currentTerm ∈ [Agents -> 0..MaxTerm]
    /\ votedFor ∈ [Agents -> Agents ∪ {"None"}]
    /\ commitIndex ∈ [Agents -> 0..MaxLog]
    /\ votesGranted ∈ [Agents -> SUBSET Agents]

\* ── Safety: at most one leader per term ──────────────────────────────────────
ElectionSafety ==
    ∀ a1, a2 ∈ Agents :
        (role[a1] = "Leader" ∧ role[a2] = "Leader" ∧ a1 ≠ a2) =>
        currentTerm[a1] ≠ currentTerm[a2]

\* ── Safety: committed entries never change ───────────────────────────────────
LogMatching ==
    ∀ a1, a2 ∈ Agents, i ∈ 1..Min(Len(log[a1]), Len(log[a2])) :
        log[a1][i].term = log[a2][i].term =>
        ∀ j ∈ 1..i : log[a1][j] = log[a2][j]

\* ── Liveness: eventually a leader is elected ─────────────────────────────────
EventuallyLeader == <>(∃ a ∈ Agents : role[a] = "Leader")

\* ── Initial state ────────────────────────────────────────────────────────────
Init ==
    /\ role = [a ∈ Agents |-> "Follower"]
    /\ currentTerm = [a ∈ Agents |-> 0]
    /\ votedFor = [a ∈ Agents |-> "None"]
    /\ log = [a ∈ Agents |-> <<>>]
    /\ commitIndex = [a ∈ Agents |-> 0]
    /\ votesGranted = [a ∈ Agents |-> {}]

Quorum == {Q ∈ SUBSET Agents : 2 * Cardinality(Q) > Cardinality(Agents)}

\* ── Actions ──────────────────────────────────────────────────────────────────
BecomeCandidate(a) ==
    /\ role[a] = "Follower"
    /\ currentTerm[a] < MaxTerm
    /\ role' = [role EXCEPT ![a] = "Candidate"]
    /\ currentTerm' = [currentTerm EXCEPT ![a] = currentTerm[a] + 1]
    /\ votedFor' = [votedFor EXCEPT ![a] = a]
    /\ votesGranted' = [votesGranted EXCEPT ![a] = {a}]
    /\ UNCHANGED <<log, commitIndex>>

GrantVote(voter, candidate) ==
    /\ role[voter] = "Follower"
    /\ currentTerm[candidate] > currentTerm[voter]
    /\ votedFor[voter] = "None"
    /\ currentTerm' = [currentTerm EXCEPT ![voter] = currentTerm[candidate]]
    /\ votedFor' = [votedFor EXCEPT ![voter] = candidate]
    /\ votesGranted' = [votesGranted EXCEPT ![candidate] = votesGranted[candidate] ∪ {voter}]
    /\ UNCHANGED <<role, log, commitIndex>>

BecomeLeader(a) ==
    /\ role[a] = "Candidate"
    /\ ∃ Q ∈ Quorum : Q ⊆ votesGranted[a]
    /\ role' = [role EXCEPT ![a] = "Leader"]
    /\ UNCHANGED <<currentTerm, votedFor, log, commitIndex, votesGranted>>

AppendEntry(leader, entry) ==
    /\ role[leader] = "Leader"
    /\ Len(log[leader]) < MaxLog
    /\ log' = [log EXCEPT ![leader] = Append(log[leader], [term |-> currentTerm[leader], value |-> entry])]
    /\ UNCHANGED <<role, currentTerm, votedFor, commitIndex, votesGranted>>

CommitEntry(leader) ==
    /\ role[leader] = "Leader"
    /\ Len(log[leader]) > commitIndex[leader]
    /\ commitIndex' = [commitIndex EXCEPT ![leader] = commitIndex[leader] + 1]
    /\ UNCHANGED <<role, currentTerm, votedFor, log, votesGranted>>

Next ==
    \/ ∃ a ∈ Agents : BecomeCandidate(a)
    \/ ∃ voter, cand ∈ Agents : voter ≠ cand ∧ GrantVote(voter, cand)
    \/ ∃ a ∈ Agents : BecomeLeader(a)
    \/ ∃ leader ∈ Agents, entry ∈ {"propose", "commit"} : AppendEntry(leader, entry)
    \/ ∃ leader ∈ Agents : CommitEntry(leader)

Spec == Init ∧ □[Next]_<<role,currentTerm,votedFor,log,commitIndex,votesGranted>>

\* ── Properties to check ──────────────────────────────────────────────────────
THEOREM Spec => □TypeInvariant
THEOREM Spec => □ElectionSafety
THEOREM Spec => □LogMatching

====
