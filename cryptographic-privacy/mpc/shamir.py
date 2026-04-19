"""
Shamir Secret Sharing over GF(256).
Full implementation without external crypto dependencies.
"""
from __future__ import annotations
import os
import secrets

# ── GF(256) arithmetic using AES field polynomial x^8+x^4+x^3+x+1 ──────────
_LOG: list[int] = [0] * 256
_EXP: list[int] = [0] * 512

def _init_tables():
    x = 1
    for i in range(255):
        _EXP[i] = x
        _LOG[x] = i
        x <<= 1
        if x & 0x100:
            x ^= 0x11B  # AES irreducible polynomial
    for i in range(255, 512):
        _EXP[i] = _EXP[i - 255]

_init_tables()


def _gf_mul(a: int, b: int) -> int:
    if a == 0 or b == 0:
        return 0
    return _EXP[(_LOG[a] + _LOG[b]) % 255]


def _gf_div(a: int, b: int) -> int:
    if b == 0:
        raise ZeroDivisionError("Division by zero in GF(256)")
    if a == 0:
        return 0
    return _EXP[(_LOG[a] - _LOG[b]) % 255]


def _gf_pow(a: int, exp: int) -> int:
    if exp == 0:
        return 1
    if a == 0:
        return 0
    return _EXP[(_LOG[a] * exp) % 255]


# ── Polynomial helpers ────────────────────────────────────────────────────────

def _eval_poly(coeffs: list[int], x: int) -> int:
    """Evaluate polynomial at x in GF(256), coeffs[0] = constant term."""
    result = 0
    for coeff in reversed(coeffs):
        result = _gf_mul(result, x) ^ coeff
    return result


def _random_poly(secret_byte: int, degree: int) -> list[int]:
    coeffs = [secret_byte] + [secrets.randbelow(256) for _ in range(degree)]
    return coeffs


# ── Public API ────────────────────────────────────────────────────────────────

class ShamirShare:
    def __init__(self, index: int, data: bytes, threshold: int, total: int):
        self.index = index       # 1-based x-coordinate
        self.data = data         # y-values for each secret byte
        self.threshold = threshold
        self.total = total

    def __repr__(self) -> str:
        return f"ShamirShare(index={self.index}, bytes={len(self.data)}, t={self.threshold}/{self.total})"


def split(secret: bytes, threshold: int, total: int) -> list[ShamirShare]:
    """Split `secret` into `total` shares, requiring `threshold` to reconstruct."""
    if threshold < 2:
        raise ValueError("Threshold must be >= 2")
    if threshold > total:
        raise ValueError("Threshold cannot exceed total shares")
    if total > 255:
        raise ValueError("Total shares cannot exceed 255")

    shares_data: list[bytearray] = [bytearray() for _ in range(total)]
    for byte in secret:
        poly = _random_poly(byte, threshold - 1)
        for i, x in enumerate(range(1, total + 1)):
            shares_data[i].append(_eval_poly(poly, x))

    return [ShamirShare(i + 1, bytes(shares_data[i]), threshold, total) for i in range(total)]


def combine(shares: list[ShamirShare]) -> bytes:
    """Reconstruct secret from `threshold` or more shares."""
    if not shares:
        raise ValueError("No shares provided")
    k = shares[0].threshold
    if len(shares) < k:
        raise ValueError(f"Need at least {k} shares, got {len(shares)}")

    # Only use the first `k` shares
    shares = shares[:k]
    secret_len = len(shares[0].data)
    result = bytearray()

    for byte_idx in range(secret_len):
        # Lagrange interpolation at x=0
        secret_byte = 0
        for i, si in enumerate(shares):
            xi = si.index
            yi = si.data[byte_idx]
            lagrange = yi
            for j, sj in enumerate(shares):
                if i == j:
                    continue
                xj = sj.index
                lagrange = _gf_mul(lagrange, xj)
                lagrange = _gf_div(lagrange, xi ^ xj)
            secret_byte ^= lagrange
        result.append(secret_byte)

    return bytes(result)


if __name__ == "__main__":
    secret = b"Nexus HyperIntelligence Secret!"
    print(f"Original: {secret!r}")
    shares = split(secret, threshold=3, total=5)
    for s in shares:
        print(f"  {s} data[:4]={s.data[:4].hex()}")
    recovered = combine(shares[:3])
    print(f"Recovered: {recovered!r}")
    print(f"Match: {secret == recovered}")
