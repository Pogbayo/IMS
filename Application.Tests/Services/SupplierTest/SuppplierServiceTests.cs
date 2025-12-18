using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Application.Services;
using IMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;


namespace Application.Tests.Services.SupplierTest
{
    public class SupplierServiceTests
    {
        // ------------------ MOCKS ------------------
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<IAppDbContext> _mockContext;
        private readonly Mock<ILogger<SupplierService>> _mockLogger;
        private readonly Mock<ICurrentUserService> _mockCurrentUser;
        private readonly Mock<IAuditService> _mockAudit;
        private readonly Mock<ICustomMemoryCache> _mockCache;

        private readonly SupplierService _service; // The service under test

        private readonly Guid _companyId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();

        public SupplierServiceTests()
        {
            var userStore = new Mock<IUserStore<AppUser>>();
            _mockUserManager = new Mock<UserManager<AppUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            _mockContext = new Mock<IAppDbContext>();
            _mockLogger = new Mock<ILogger<SupplierService>>();
            _mockCurrentUser = new Mock<ICurrentUserService>();
            _mockAudit = new Mock<IAuditService>();
            _mockCache = new Mock<ICustomMemoryCache>();

            // Simulate current user
            _mockCurrentUser.Setup(x => x.GetCurrentUserId()).Returns(_userId);

            // Initialize service
            _service = new SupplierService(
                _mockUserManager.Object,
                _mockContext.Object,
                _mockLogger.Object,
                _mockCurrentUser.Object,
                _mockAudit.Object,
                _mockCache.Object
            );
        }

        #region DeleteSupplier Tests

        [Fact]
        public async Task DeleteSupplier_InvalidId_ReturnsFailure()
        {
            var result = await _service.DeleteSupplier(Guid.Empty);

            Assert.False(result.Success);
            Assert.Equal("Supplier ID cannot be empty.", result.Message);
        }

        [Fact]
        public async Task DeleteSupplier_SupplierExists_RemovesSupplierAndReturnsSuccess()
        {
            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Test Supplier", CompanyId = _companyId };
            var suppliers = new List<Supplier> { supplier }.AsQueryable();

            var mockSet = new Mock<DbSet<Supplier>>();
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Provider).Returns(suppliers.Provider);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Expression).Returns(suppliers.Expression);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.ElementType).Returns(suppliers.ElementType);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.GetEnumerator()).Returns(suppliers.GetEnumerator());

            _mockContext.Setup(c => c.Suppliers).Returns(mockSet.Object);
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Mock cache
            _mockCache.Setup(c => c.RemoveByPrefix(It.IsAny<string>())).Verifiable();

            var result = await _service.DeleteSupplier(supplier.Id);

            Assert.True(result.Success);
            Assert.Equal("Supplier deleted successfully.", result.Message);
            _mockCache.Verify(c => c.RemoveByPrefix(It.Is<string>(s => s.StartsWith($"Suppliers_{_companyId}_"))), Times.Once);
        }

        #endregion

        #region GetAllSuppliers Tests

        [Fact]
        public async Task GetAllSuppliers_ReturnsSuppliers_FromCache()
        {
            var cachedSuppliers = new List<SupplierDto>
            {
                new SupplierDto { Id = Guid.NewGuid(), Name = "Cached Supplier", Email = "a@test.com", PhoneNumber = "123" }
            };

            _mockCache.Setup(c => c.GetOrCreateAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<ICacheEntry, Task<List<SupplierDto>>>>()))
                .ReturnsAsync(cachedSuppliers);

            var result = await _service.GetAllSuppliers();

            Assert.True(result.Success);
            Assert.Single(result.Data);
            Assert.Equal("Cached Supplier", result.Data[0].Name);
        }

        #endregion

        #region RegisterSupplierToCompany Tests

        [Fact]
        public async Task RegisterSupplierToCompany_ValidDto_CreatesSupplier()
        {
            var dto = new SupplierCreateDto { Name = "New Supplier", Email = "new@test.com", Phone = "" };
            var company = new Company { Id = _companyId, Name = "TestCo", Email = "info@testco.com" };

            _mockContext.Setup(c => c.Companies.FindAsync(_companyId)).ReturnsAsync(company);
            _mockContext.Setup(c => c.Suppliers.Add(It.IsAny<Supplier>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockCache.Setup(c => c.RemoveByPrefix(It.IsAny<string>())).Verifiable();

            var result = await _service.RegisterSupplierToCompany(_companyId, dto);

            Assert.True(result.Success);
            Assert.Equal("Supplier created successfully", result.Message);
            _mockContext.Verify(c => c.Suppliers.Add(It.Is<Supplier>(s => s.Name == "New Supplier")), Times.Once);
            _mockCache.Verify(c => c.RemoveByPrefix(It.Is<string>(s => s.StartsWith($"Suppliers_{_companyId}_"))), Times.Once);
        }

        #endregion

        #region UpdateSupplier Tests

        [Fact]
        public async Task UpdateSupplier_ValidDto_UpdatesSupplier()
        {
            var supplierId = Guid.NewGuid();
            var supplier = new Supplier { Id = supplierId, Name = "Old Name", CompanyId = _companyId };
            var suppliers = new List<Supplier> { supplier }.AsQueryable();

            var mockSet = new Mock<DbSet<Supplier>>();
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Provider).Returns(suppliers.Provider);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Expression).Returns(suppliers.Expression);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.ElementType).Returns(suppliers.ElementType);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.GetEnumerator()).Returns(suppliers.GetEnumerator());

            _mockContext.Setup(c => c.Suppliers).Returns(mockSet.Object);
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockCache.Setup(c => c.RemoveByPrefix(It.IsAny<string>())).Verifiable();

            var dto = new SupplierUpdateDto { Id = supplierId, Name = "New Name" };
            var result = await _service.UpdateSupplier(dto);

            Assert.True(result.Success);
            Assert.Equal("Supplier updated successfully.", result.Message);
            _mockCache.Verify(c => c.RemoveByPrefix(It.Is<string>(s => s.StartsWith($"Suppliers_{_companyId}_"))), Times.Once);
        }

        #endregion

        #region GetSupplierByName Tests

        [Fact]
        public async Task GetSupplierByName_Found_ReturnsSupplier()
        {
            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Found Supplier", CompanyId = _companyId };
            var suppliers = new List<Supplier> { supplier }.AsQueryable();

            var mockSet = new Mock<DbSet<Supplier>>();
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Provider).Returns(suppliers.Provider);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.Expression).Returns(suppliers.Expression);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.ElementType).Returns(suppliers.ElementType);
            mockSet.As<IQueryable<Supplier>>().Setup(m => m.GetEnumerator()).Returns(suppliers.GetEnumerator());

            _mockContext.Setup(c => c.Suppliers).Returns(mockSet.Object);

            _mockCache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<ICacheEntry, Task<SupplierDto>>>()))
                .ReturnsAsync(new SupplierDto { Id = supplier.Id, Name = supplier.Name, Email = "a@test.com", PhoneNumber = "123" });

            var result = await _service.GetSupplierByName("Found Supplier");

            Assert.True(result.Success);
            Assert.Equal("Found Supplier", result.Data.Name);
        }

        #endregion
    }
}
