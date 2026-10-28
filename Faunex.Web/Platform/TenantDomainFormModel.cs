using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Platform;

public sealed class TenantDomainFormModel
{
    [Required, StringLength(253)]
    public string Hostname { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
