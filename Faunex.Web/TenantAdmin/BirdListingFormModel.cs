using System.ComponentModel.DataAnnotations;
using Faunex.Application.DTOs;

namespace Faunex.Web.TenantAdmin;

public sealed class BirdListingFormModel
{
    [Required, StringLength(160, MinimumLength = 3)] public string Title { get; set; } = "";
    [Required] public Guid? SpeciesId { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true)] public decimal Price { get; set; } = 1;
    public string SaleType { get; set; } = "auction";
    public string CurrencyCode { get; set; } = "ZAR";
    [Range(1, 10000)] public int Quantity { get; set; } = 1;
    [StringLength(200)] public string? Location { get; set; }
    [StringLength(80)] public string? RingNumber { get; set; }
    [StringLength(80)] public string? MicrochipNumber { get; set; }
    public string Sex { get; set; } = "Unknown";
    public DateOnly? HatchDate { get; set; }
    public static BirdListingFormModel FromDto(BirdListingDto dto) => new()
    {
        Title = dto.Title, SpeciesId = dto.SpeciesId, Description = dto.Description,
        Price = dto.BuyNowPrice ?? dto.StartingPrice, SaleType = dto.BuyNowPrice.HasValue ? "fixed" : "auction",
        CurrencyCode = dto.CurrencyCode, Quantity = dto.Quantity, Location = dto.Location,
        RingNumber = dto.RingNumber, MicrochipNumber = dto.MicrochipNumber,
        Sex = dto.Sex ?? "Unknown", HatchDate = dto.HatchDate
    };
}

public sealed record BirdSpeciesChoice(Guid Id, string ScientificName, string CommonName);
