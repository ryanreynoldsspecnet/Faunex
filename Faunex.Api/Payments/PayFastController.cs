using System.Net;
using System.Security.Cryptography;
using System.Text;
using Faunex.Api.Tenancy;
using Faunex.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Payments;

[ApiController, Route("api/payments/payfast"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayFastController(PayFastService payments, PayFastRuntime runtime, IPayFastVerifier verifier) : ControllerBase
{
    [HttpPost("orders/{id:guid}/checkout"), Authorize(Roles = "Buyer")]
    public async Task<ActionResult<PayFastCheckoutDto>> Checkout(Guid id, CancellationToken ct) => await payments.PrepareAsync(id, ct);

    [HttpPost("notify"), AllowAnonymous, InfrastructureEndpoint, PreservePayFastForm, RequestSizeLimit(16384), EnableRateLimiting("payfast-notifications")]
    public async Task<IActionResult> Notify(CancellationToken ct)
    {
        var supplied = Request.Headers[PayFastRuntime.IngressHeader]; var source = Request.Headers[PayFastRuntime.SourceHeader];
        if (runtime.IngressToken.Length < 32) return StatusCode(503);
        if (supplied.Count != 1 || source.Count != 1 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(runtime.IngressToken)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied[0] ?? "")))
            || !IPAddress.TryParse(source[0], out var address) || !verifier.IsSourceAllowed(address)) return BadRequest();
        if (!string.Equals(Request.ContentType?.Split(';')[0].Trim(), "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) return BadRequest();
        try { await payments.NotifyAsync(await PayFastProtocol.ReadNotificationAsync(Request.Body, ct), ct); return Ok(); }
        catch (InvalidDataException) { return BadRequest(); }
        catch (DecoderFallbackException) { return BadRequest(); }
        catch (DbUpdateConcurrencyException) { Response.Headers.RetryAfter = "5"; return StatusCode(503); }
    }
}

// MVC's form value providers otherwise consume the wire body before the ordered signature parser.
[AttributeUsage(AttributeTargets.Method)]
public sealed class PreservePayFastFormAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        for (var i = context.ValueProviderFactories.Count - 1; i >= 0; i--)
            if (context.ValueProviderFactories[i] is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory)
                context.ValueProviderFactories.RemoveAt(i);
        await next();
    }
}
