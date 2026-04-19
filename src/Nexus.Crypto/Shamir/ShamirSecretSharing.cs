using System.Security.Cryptography;
using Nexus.Models;

namespace Nexus.Crypto.Shamir;

/// <summary>
/// Shamir's Secret Sharing over GF(256) — 3-of-5 default, configurable up to 255 shares.
/// </summary>
public sealed class ShamirSecretSharing
{
    private static readonly byte[] _gfLog   = new byte[256];
    private static readonly byte[] _gfAntiLog = new byte[512];

    static ShamirSecretSharing()
    {
        // Build GF(2^8) log / antilog tables with primitive polynomial x^8+x^4+x^3+x^2+1
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            _gfAntiLog[i] = (byte)x;
            _gfLog[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0) x ^= 0x11b;
        }
        _gfAntiLog[255] = _gfAntiLog[0];
        for (int i = 256; i < 512; i++) _gfAntiLog[i] = _gfAntiLog[i - 255];
    }

    private static byte GfMul(byte a, byte b)
    {
        if (a == 0 || b == 0) return 0;
        return _gfAntiLog[_gfLog[a] + _gfLog[b]];
    }

    private static byte GfDiv(byte a, byte b)
    {
        if (b == 0) throw new DivideByZeroException("GF division by zero");
        if (a == 0) return 0;
        return _gfAntiLog[255 + _gfLog[a] - _gfLog[b]];
    }

    private static byte GfPow(byte a, byte b)
    {
        if (b == 0) return 1;
        if (a == 0) return 0;
        return _gfAntiLog[_gfLog[a] * b % 255];
    }

    /// <summary>Split secret into n shares requiring threshold to recover.</summary>
    public List<ShamirShare> Split(byte[] secret, int threshold = 3, int totalShares = 5)
    {
        if (threshold < 2 || threshold > totalShares)
            throw new ArgumentException("Invalid threshold/shares configuration");
        if (totalShares > 255)
            throw new ArgumentException("Maximum 255 shares supported");

        var shares = Enumerable.Range(1, totalShares)
            .Select(i => new ShamirShare { Index = i, Share = new byte[secret.Length], Threshold = threshold, TotalShares = totalShares })
            .ToList();

        for (int byteIdx = 0; byteIdx < secret.Length; byteIdx++)
        {
            // Random polynomial of degree (threshold-1) with secret at x=0
            var coefficients = new byte[threshold];
            coefficients[0] = secret[byteIdx];
            RandomNumberGenerator.Fill(coefficients.AsSpan(1));

            for (int shareIdx = 0; shareIdx < totalShares; shareIdx++)
            {
                byte x = (byte)(shareIdx + 1);
                byte y = 0;
                byte xPow = 1;
                for (int k = 0; k < threshold; k++)
                {
                    y ^= GfMul(coefficients[k], xPow);
                    xPow = GfMul(xPow, x);
                }
                shares[shareIdx].Share[byteIdx] = y;
            }
        }
        return shares;
    }

    /// <summary>Recover secret from at least threshold shares (Lagrange interpolation in GF(256)).</summary>
    public byte[] Combine(List<ShamirShare> shares)
    {
        if (shares.Count < 2) throw new ArgumentException("Need at least 2 shares");
        int secretLen = shares[0].Share.Length;
        if (shares.Any(s => s.Share.Length != secretLen))
            throw new ArgumentException("All shares must have the same length");

        var secret = new byte[secretLen];
        var xs = shares.Select(s => (byte)s.Index).ToArray();

        for (int byteIdx = 0; byteIdx < secretLen; byteIdx++)
        {
            byte value = 0;
            for (int i = 0; i < shares.Count; i++)
            {
                byte num = 1, den = 1;
                for (int j = 0; j < shares.Count; j++)
                {
                    if (i == j) continue;
                    num = GfMul(num, xs[j]);
                    den = GfMul(den, (byte)(xs[i] ^ xs[j]));
                }
                value ^= GfMul(GfDiv(num, den), shares[i].Share[byteIdx]);
            }
            secret[byteIdx] = value;
        }
        return secret;
    }
}
