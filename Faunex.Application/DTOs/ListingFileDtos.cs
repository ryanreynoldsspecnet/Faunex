using Faunex.Domain.Entities;

namespace Faunex.Application.DTOs;

public sealed record ListingFileDto(Guid Id, string FileName, string ContentType, long Length,
    bool IsPhoto, ListingDocumentType? DocumentType, DateTimeOffset UploadedAt);
public sealed record ListingFilesDto(Guid ListingId, Guid Revision, string Status, bool CanModify, IReadOnlyList<ListingFileDto> Files, string? Title = null);
