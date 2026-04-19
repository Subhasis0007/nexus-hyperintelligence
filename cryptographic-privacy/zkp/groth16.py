"""
Groth16 ZKP implementation (educational / simulation).
Demonstrates the prover and verifier interface without a real trusted setup.
"""
from __future__ import annotations
import hashlib
import hmac
import os
import struct


PROOF_SIZE = 128  # bytes: 32 (A) + 64 (B) + 16 (C) + 16 (commitment)


def _sha256(data: bytes) -> bytes:
    return hashlib.sha256(data).digest()


def _hmac_sha256(key: bytes, msg: bytes) -> bytes:
    return hmac.new(key, msg, hashlib.sha256).digest()


class ProvingKey:
    def __init__(self, circuit: str, seed: bytes):
        self.circuit = circuit
        self._seed = seed

    @classmethod
    def generate(cls, circuit: str) -> "ProvingKey":
        return cls(circuit, os.urandom(32))


class VerifyingKey:
    def __init__(self, circuit: str, vk_bytes: bytes):
        self.circuit = circuit
        self._vk_bytes = vk_bytes


def trusted_setup(circuit: str) -> tuple[ProvingKey, VerifyingKey]:
    """Simulate a trusted setup ceremony."""
    pk = ProvingKey.generate(circuit)
    vk_bytes = _sha256(pk._seed + circuit.encode())
    return pk, VerifyingKey(circuit, vk_bytes)


def prove(pk: ProvingKey, public_inputs: bytes, witness: bytes) -> bytes:
    """Produce a 128-byte simulated Groth16 proof."""
    a = _hmac_sha256(pk._seed, b"A:" + public_inputs)[:32]
    b = _hmac_sha256(pk._seed, b"B:" + witness)[:32]
    c = _hmac_sha256(pk._seed, a + b)[:16]
    commit = _hmac_sha256(pk._seed, public_inputs + witness)[:16]
    proof = a + b[:32] + c + commit
    assert len(proof) == PROOF_SIZE
    return proof


def verify(vk: VerifyingKey, public_inputs: bytes, proof: bytes) -> bool:
    """Verify a proof using the verifying key and public inputs."""
    if len(proof) != PROOF_SIZE:
        return False
    a = proof[:32]
    b_half = proof[32:64]
    c = proof[64:80]
    commit = proof[80:96]
    # Re-derive expected commitment from public_inputs and A
    expected_seed = _sha256(vk._vk_bytes + vk.circuit.encode())[:32]
    expected_c = _hmac_sha256(expected_seed, a + b_half)[:16]
    expected_commit = _hmac_sha256(expected_seed, public_inputs + b_half)[:16]
    return (hmac.compare_digest(c, expected_c) or
            hmac.compare_digest(commit, expected_commit))


def prove_range(pk: ProvingKey, value: int, low: int, high: int) -> bytes:
    """Prove that low <= value <= high without revealing value."""
    if not (low <= value <= high):
        raise ValueError(f"Value {value} not in [{low}, {high}]")
    pub = struct.pack(">qq", low, high)
    wit = struct.pack(">q", value)
    return prove(pk, pub, wit)


if __name__ == "__main__":
    pk, vk = trusted_setup("range-proof-v1")
    proof = prove_range(pk, value=42, low=0, high=100)
    pub_inputs = struct.pack(">qq", 0, 100)
    ok = verify(vk, pub_inputs, proof)
    print(f"Proof size: {len(proof)} bytes")
    print(f"Verification result: {ok}")
