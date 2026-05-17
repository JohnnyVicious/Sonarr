using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Notifications.Pushover;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class PushoverEncryptionFixture
    {
        // Fixed 256-bit test key (64 hex chars)
        private static readonly byte[] TestKey = Convert.FromHexString("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        [Test]
        public void should_produce_valid_pushover_e2ee_wire_format()
        {
            // Pushover E2EE spec (pushover.net/api#e2ee):
            //   Base64( IV[16] || AES-256-CBC-ciphertext || HMAC-SHA256[32] )
            //   HMAC covers IV || ciphertext, using the SAME key as encryption.

            var result = PushoverProxy.EncryptField("Hello Pushover", TestKey);

            var payload = Convert.FromBase64String(result);

            // Minimum size: 16 (IV) + 16 (at least one AES block) + 32 (HMAC)
            payload.Length.Should().BeGreaterOrEqualTo(64);

            // Extract components
            var iv = payload[..16];
            var ciphertext = payload[16..^32];
            var receivedMac = payload[^32..];

            // Verify HMAC-SHA256 over (IV || ciphertext) with the SAME key
            using var hmac = new HMACSHA256(TestKey);
            var expectedMac = hmac.ComputeHash(payload[..^32]);
            receivedMac.Should().Equal(expectedMac, "HMAC must cover IV+ciphertext using the encryption key (Pushover spec)");

            // Verify decryption produces GZIP-compressed plaintext
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = TestKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var decompressed = GzipDecompress(decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length));
            var decrypted = Encoding.UTF8.GetString(decompressed);

            decrypted.Should().Be("Hello Pushover");
        }

        [Test]
        public void should_use_aes_256_cbc_not_other_modes()
        {
            // Encrypt same plaintext twice — different IVs means different output
            // (GCM would also do this, but CBC is what the spec requires)
            var result1 = PushoverProxy.EncryptField("test", TestKey);
            var result2 = PushoverProxy.EncryptField("test", TestKey);

            result1.Should().NotBe(result2, "each encryption must use a random IV");

            // Both must still decrypt to the same plaintext
            DecryptPushoverPayload(result1, TestKey).Should().Be("test");
            DecryptPushoverPayload(result2, TestKey).Should().Be("test");
        }

        [Test]
        public void should_use_same_key_for_encryption_and_hmac()
        {
            // The Pushover spec mandates that HMAC uses the SAME key as AES.
            // If someone "fixes" this by deriving separate keys, this test will fail.
            var result = PushoverProxy.EncryptField("verify key reuse", TestKey);
            var payload = Convert.FromBase64String(result);

            var receivedMac = payload[^32..];

            // Verify with encryption key — must match
            using var hmac = new HMACSHA256(TestKey);
            var expectedMac = hmac.ComputeHash(payload[..^32]);
            receivedMac.Should().Equal(expectedMac,
                "Pushover spec requires HMAC key == encryption key (pushover.net/api#e2ee)");
        }

        [Test]
        public void should_handle_empty_plaintext()
        {
            var result = PushoverProxy.EncryptField("", TestKey);
            DecryptPushoverPayload(result, TestKey).Should().Be("");
        }

        [Test]
        public void should_handle_null_plaintext()
        {
            var result = PushoverProxy.EncryptField(null, TestKey);
            DecryptPushoverPayload(result, TestKey).Should().Be("");
        }

        private static string DecryptPushoverPayload(string base64Payload, byte[] key)
        {
            var payload = Convert.FromBase64String(base64Payload);
            var iv = payload[..16];
            var ciphertext = payload[16..^32];

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var compressed = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(GzipDecompress(compressed));
        }

        private static byte[] GzipDecompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
