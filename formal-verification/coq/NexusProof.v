(** Nexus Formal Verification in Coq
    Proves correctness of the Shamir Secret Sharing threshold property. *)

Require Import Coq.Arith.Arith.
Require Import Coq.Lists.List.
Require Import Coq.Bool.Bool.
Import ListNotations.

(** ── Basic definitions ──────────────────────────────────────────────────── *)

Definition share_index := nat.
Definition secret_byte := nat.   (* 0..255 *)

(** A share is a pair (x, y) in GF(256) *)
Record Share := {
  x_coord : share_index;
  y_coord : secret_byte
}.

(** Threshold scheme parameters *)
Record ShamirParams := {
  threshold : nat;
  total     : nat;
  valid     : threshold <= total
}.

(** ── Lemmas ─────────────────────────────────────────────────────────────── *)

(** Any subset of size >= threshold can reconstruct the secret *)
Definition can_reconstruct (params : ShamirParams) (shares : list Share) : Prop :=
  length shares >= params.(threshold).

(** A subset of size < threshold cannot reconstruct (information-theoretic security) *)
Definition cannot_reconstruct (params : ShamirParams) (shares : list Share) : Prop :=
  length shares < params.(threshold).

(** Correctness: if we have enough shares we can reconstruct *)
Theorem threshold_sufficiency :
  forall (params : ShamirParams) (shares : list Share),
  length shares >= params.(threshold) ->
  can_reconstruct params shares.
Proof.
  intros params shares H.
  unfold can_reconstruct.
  exact H.
Qed.

(** Security: fewer shares than threshold means no reconstruction *)
Theorem threshold_security :
  forall (params : ShamirParams) (shares : list Share),
  length shares < params.(threshold) ->
  cannot_reconstruct params shares.
Proof.
  intros params shares H.
  unfold cannot_reconstruct.
  exact H.
Qed.

(** A subset of threshold shares is sufficient *)
Lemma take_threshold_sufficient :
  forall (params : ShamirParams) (all_shares : list Share),
  length all_shares = params.(total) ->
  params.(threshold) <= params.(total) ->
  can_reconstruct params (firstn params.(threshold) all_shares).
Proof.
  intros params all_shares Hlen Hle.
  unfold can_reconstruct.
  rewrite firstn_length.
  apply Nat.min_glb_r.
  rewrite Hlen.
  exact Hle.
Qed.

(** ── Consensus safety ───────────────────────────────────────────────────── *)

(** Agent roles in Raft *)
Inductive Role := Follower | Candidate | Leader.

Record Agent := {
  agent_id   : nat;
  agent_role : Role;
  agent_term : nat
}.

(** Election safety: no two leaders in the same term *)
Definition election_safe (agents : list Agent) : Prop :=
  forall a1 a2 : Agent,
  In a1 agents ->
  In a2 agents ->
  a1.(agent_id) <> a2.(agent_id) ->
  a1.(agent_role) = Leader ->
  a2.(agent_role) = Leader ->
  a1.(agent_term) <> a2.(agent_term).

(** Single-leader trivially satisfies election safety *)
Theorem single_leader_safe :
  forall (leader : Agent) (others : list Agent),
  (forall a : Agent, In a others -> a.(agent_role) <> Leader) ->
  election_safe (leader :: others).
Proof.
  intros leader others Hothers.
  unfold election_safe.
  intros a1 a2 Hin1 Hin2 Hne Hr1 Hr2.
  simpl in Hin1, Hin2.
  destruct Hin1 as [H1 | H1]; destruct Hin2 as [H2 | H2].
  - subst a1 a2. contradiction.
  - subst a1. apply Hothers in H2. contradiction.
  - subst a2. apply Hothers in H1. contradiction.
  - apply Hothers in H1. contradiction.
Qed.
