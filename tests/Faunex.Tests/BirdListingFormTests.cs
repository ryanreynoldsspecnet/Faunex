using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Faunex.Web.TenantAdmin;

namespace Faunex.Tests;

public sealed class BirdListingFormTests
{
    [Theory]
    [InlineData("en-ZA")]
    [InlineData("af-ZA")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void Price_validation_does_not_crash_when_culture_uses_a_decimal_comma(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var model = new BirdListingFormModel { Title = "African grey", SpeciesId = Guid.NewGuid(), Price = 1500.50m };
            Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), [], true));
            model.Price = 0;
            Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), [], true));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
