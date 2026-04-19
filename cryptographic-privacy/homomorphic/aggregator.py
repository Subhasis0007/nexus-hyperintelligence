"""
Additive homomorphic encryption (Paillier-style simulation).
Allows computing sums over encrypted values without decryption.
"""
from __future__ import annotations
import struct
import hashlib
import os


# ── Key generation ────────────────────────────────────────────────────────────
class PublicKey:
    def __init__(self, n: int, g: int):
        self.n = n
        self.g = g
        self.n_sq = n * n


class PrivateKey:
    def __init__(self, lam: int, mu: int, pub: PublicKey):
        self.lam = lam
        self.mu = mu
        self.pub = pub


def _l_func(x: int, n: int) -> int:
    return (x - 1) // n


def generate_keys(bits: int = 512) -> tuple[PublicKey, PrivateKey]:
    """
    Simulated key generation — uses large-ish primes from deterministic seed.
    For production, use a proper cryptographic library.
    """
    # Deterministic "large" primes for reproducibility in tests
    if bits <= 64:
        p, q = 61, 53
    elif bits <= 128:
        p, q = 1019, 1013
    else:
        # Use seed-based deterministic primes for simulation
        p = (1 << 255) - 19          # Curve25519 prime
        q = (1 << 127) - 1           # Mersenne prime M127
    n = p * q
    lam = ((p - 1) * (q - 1)) // _gcd((p - 1), (q - 1))
    g = n + 1                        # Common choice: g = n+1
    mu = pow(_l_func(pow(g, lam, n * n), n), -1, n)
    return PublicKey(n, g), PrivateKey(lam, mu, PublicKey(n, g))


def _gcd(a: int, b: int) -> int:
    while b:
        a, b = b, a % b
    return a


# ── Encrypt / Decrypt ─────────────────────────────────────────────────────────

def encrypt(pub: PublicKey, plaintext: int) -> int:
    """Encrypt a non-negative integer."""
    if plaintext < 0 or plaintext >= pub.n:
        raise ValueError("Plaintext out of range [0, n)")
    r = int.from_bytes(os.urandom(16), "big") % (pub.n - 2) + 2
    ct = (pow(pub.g, plaintext, pub.n_sq) * pow(r, pub.n, pub.n_sq)) % pub.n_sq
    return ct


def decrypt(priv: PrivateKey, ciphertext: int) -> int:
    """Decrypt a ciphertext."""
    n = priv.pub.n
    x = pow(ciphertext, priv.lam, n * n)
    pt = (_l_func(x, n) * priv.mu) % n
    return pt


# ── Homomorphic operations ────────────────────────────────────────────────────

def add_encrypted(pub: PublicKey, ct1: int, ct2: int) -> int:
    """Add two ciphertexts homomorphically."""
    return (ct1 * ct2) % pub.n_sq


def add_plain(pub: PublicKey, ct: int, plaintext: int) -> int:
    """Add a plaintext value to a ciphertext."""
    return (ct * pow(pub.g, plaintext, pub.n_sq)) % pub.n_sq


def multiply_plain(pub: PublicKey, ct: int, scalar: int) -> int:
    """Multiply a ciphertext by a scalar."""
    return pow(ct, scalar, pub.n_sq)


def aggregate(pub: PublicKey, ciphertexts: list[int]) -> int:
    """Aggregate (sum) a list of ciphertexts."""
    result = 1
    for ct in ciphertexts:
        result = (result * ct) % pub.n_sq
    return result


if __name__ == "__main__":
    pub, priv = generate_keys(128)
    values = [10, 20, 30, 40, 50]
    encrypted = [encrypt(pub, v) for v in values]
    total_ct = aggregate(pub, encrypted)
    total = decrypt(priv, total_ct)
    print(f"Values: {values}")
    print(f"Expected sum: {sum(values)}")
    print(f"Decrypted sum: {total}")
    print(f"Correct: {sum(values) == total}")
    avg = total / len(values)
    print(f"Average: {avg}")
