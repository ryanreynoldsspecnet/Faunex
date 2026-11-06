namespace Faunex.Application.Auth;

public sealed record AdminMfaStatus(bool Enabled, int RecoveryCodesRemaining);
public sealed record AdminMfaStart(string Password);
public sealed record AdminMfaSetup(string Ticket, string ManualKey, string QrPngBase64);
public sealed record AdminMfaConfirm(string Ticket, string Code);
public sealed record AdminMfaRecovery(IReadOnlyList<string> Codes);
public sealed record AdminMfaChallenge(string MfaChallenge);
public sealed record AdminMfaLogin(string Challenge, string Code, bool RecoveryCode = false);
