using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IMS.Application.DTO.Company
{
    public class AllCompaniesDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string HeadOffice { get; set; } = string.Empty;
    }
}
