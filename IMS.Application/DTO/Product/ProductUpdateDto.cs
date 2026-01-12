using Microsoft.AspNetCore.Http;

namespace IMS.Application.DTO.Product
{
    public class ProductUpdateDto
    {
        public string? Name { get; set; }
        public IFormFile? ImgUrl { get; set; }
        public decimal? Price { get; set; }
    }
}