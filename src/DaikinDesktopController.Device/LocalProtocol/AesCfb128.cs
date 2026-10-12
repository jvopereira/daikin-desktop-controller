using System.Security.Cryptography;
namespace DaikinDesktopController.Device.LocalProtocol;
internal static class AesCfb128
{
    public static byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (iv.Length != 16) throw new ArgumentException("Invalid IV length.", nameof(iv));
        using var aes = Aes.Create();
        aes.Key = key.ToArray();
        var feedback = iv.ToArray();
        var output = new byte[ciphertext.Length];
        Span<byte> keystream = stackalloc byte[16];
        try
        {
            for (var offset = 0; offset < ciphertext.Length; offset += 16)
            {
                aes.EncryptEcb(feedback, keystream, PaddingMode.None);
                var length = Math.Min(16, ciphertext.Length - offset);
                for (var i = 0; i < length; i++) output[offset + i] = (byte)(ciphertext[offset + i] ^ keystream[i]);
                if (length == 16) ciphertext.Slice(offset, 16).CopyTo(feedback);
            }
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(feedback); CryptographicOperations.ZeroMemory(keystream); }
    }
    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (iv.Length != 16) throw new ArgumentException("Invalid IV length.", nameof(iv));
        using var aes = Aes.Create();
        aes.Key = key.ToArray();
        var feedback = iv.ToArray();
        var output = new byte[plaintext.Length];
        Span<byte> keystream = stackalloc byte[16];
        try
        {
            for (var offset = 0; offset < plaintext.Length; offset += 16)
            {
                aes.EncryptEcb(feedback, keystream, PaddingMode.None);
                var length = Math.Min(16, plaintext.Length - offset);
                for (var i = 0; i < length; i++) output[offset + i] = (byte)(plaintext[offset + i] ^ keystream[i]);
                if (length == 16) output.AsSpan(offset, 16).CopyTo(feedback);
            }
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(feedback); CryptographicOperations.ZeroMemory(keystream); }
    }
}
