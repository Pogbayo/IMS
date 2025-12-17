namespace IMS.Application.DTO.Product
{
    public sealed record ProductsDto(
     Guid Id,
     string? Name,
     string? SKU,
     string? ImgUrl,
     decimal RetailPrice
     );

}
