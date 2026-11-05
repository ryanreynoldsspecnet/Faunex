using Faunex.Application.DTOs;

namespace Faunex.Web.Payments;

public static class OrderPaymentDisplay
{
    public static string Label(string stage) => stage switch
    {
        OrderPaymentStages.Pricing => "Pricing needed",
        OrderPaymentStages.Unpaid => "Payment not started",
        OrderPaymentStages.Pending => "Awaiting confirmation",
        OrderPaymentStages.Confirmed => "Sandbox confirmed",
        _ => "Payment review required"
    };

    public static string SummaryLabel(string status) => status switch
    {
        "Pending" => Label(OrderPaymentStages.Pending),
        "SandboxComplete" => "Sandbox payment confirmed",
        _ => Label(OrderPaymentStages.Review)
    };
}
