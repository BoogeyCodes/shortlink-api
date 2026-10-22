using System.Security.Cryptography;

namespace ShortLink.Api.Infrastructure;

public interface ICodeGenerator
{
    string Generate(int length);
}

/// <summary>
/// Generates URL-safe short codes using a cryptographic RNG.
/// The alphabet deliberately omits characters that are easy to confuse (0/O, 1/l/I).
/// </summary>
public sealed class CodeGenerator : ICodeGenerator
{
    public const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";

    public string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        return string.Create(length, Alphabet, static (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }
}
