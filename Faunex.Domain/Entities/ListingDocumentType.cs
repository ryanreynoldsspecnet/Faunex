namespace Faunex.Domain.Entities;

public enum ListingDocumentType
{
    Unknown = 0,

    // Birds
    CitesPermit = 10,
    VeterinaryCertificate = 11,

    // Livestock
    HealthCertificate = 20,
    TransferOfOwnership = 21,

    // Game Animals
    GamePermit = 30,

    // Poultry
    PoultryHealthCertificate = 40,
    TransportPermit = 41
}
