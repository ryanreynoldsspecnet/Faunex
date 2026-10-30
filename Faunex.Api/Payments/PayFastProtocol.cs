using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Faunex.Application.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace Faunex.Api.Payments;

public static class PayFastProtocol
{
    public const string CheckoutUrl = "https://sandbox.payfast.co.za/eng/process";
    public const string ValidationUrl = "https://sandbox.payfast.co.za/eng/query/validate";
    // PayFast custom integration requires PHP/RFC1738 encoding, not API alphabetical ordering.
    public static string Encode(string value)
    {
        var output = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'_' or (byte)'.') output.Append((char)b);
            else if (b == 32) output.Append('+');
            else output.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return output.ToString();
    }
    public static string Canonical(IEnumerable<CheckoutFieldDto> fields) => string.Join('&', fields.Select(x => x.Name + "=" + Encode(x.Value)));
    // MD5 is mandated by the provider protocol, not used for password or storage protection.
    public static string Signature(string canonical, string passphrase) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(canonical + "&passphrase=" + Encode(passphrase))));
    public static bool ValidSignature(string expected, string actual) => Regex.IsMatch(actual, "^[a-fA-F0-9]{32}$", RegexOptions.CultureInvariant)
        && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual));
    public static async Task<List<CheckoutFieldDto>> ReadNotificationAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new byte[16385]; var length = 0;
        while (length < bytes.Length)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(length), ct);
            if (count == 0) break;
            length += count;
        }
        if (length > 16384) throw new InvalidDataException("Notification too large.");
        using var input = new MemoryStream(bytes, 0, length, false);
        using var reader = new FormReader(input, new UTF8Encoding(false, true)) { KeyLengthLimit = 64, ValueLengthLimit = 2048, ValueCountLimit = 64 };
        var fields = new List<CheckoutFieldDto>(); var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadNextPairAsync(ct) is { } pair)
        {
            if (fields.Count >= 64 || !Regex.IsMatch(pair.Key, "^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)
                || !names.Add(pair.Key) || fields.LastOrDefault()?.Name == "signature")
                throw new PaymentRejectedException("Invalid notification fields.");
            fields.Add(new(pair.Key, pair.Value));
        }
        if (fields.LastOrDefault()?.Name != "signature") throw new PaymentRejectedException("A final security signature is required.");
        return fields;
    }
}
