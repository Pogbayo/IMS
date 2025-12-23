using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Application.Services;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace IMS.Application.Tests.Services.SupplierServiceTest
{
    public class DeleteSupplierTests
    {
        private readonly Guid _companyId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();

        private IMS_DbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<IMS_DbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new IMS_DbContext(options);
        }

        private SupplierService CreateService(
            IMS_DbContext context,
            Mock<ICustomMemoryCache>? cacheMock = null)
        {
            cacheMock ??= new Mock<ICustomMemoryCache>();

            var userStore = new Mock<IUserStore<AppUser>>();
            var userManager = new Mock<UserManager<AppUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            userManager.Setup(u => u.Users)
                .Returns(new List<AppUser>
                {
                    new AppUser { Id = _userId, CompanyId = _companyId }
                }.AsQueryable());

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.Setup(x => x.GetCurrentUserId()).Returns(_userId);

            return new SupplierService(
                userManager.Object,
                context,
                Mock.Of<ILogger<SupplierService>>(),
                currentUser.Object,
                Mock.Of<IAuditService>(),
                cacheMock.Object
            );
        }

        //Tests
        [Fact]
        public async Task DeleteSupplier_EmptyId_ReturnsFailure()
        {
            var context = CreateContext();
            var service = CreateService(context);

            var result = await service.DeleteSupplier(Guid.Empty);

            Assert.False(result.Success);
            Assert.Equal("Supplier ID cannot be empty.", result.Message);
        }

        [Fact]
        public async Task DeleteSupplier_SupplierNotFound_ReturnsFailure()
        {
            var context = CreateContext();
            var service = CreateService(context);

            var result = await service.DeleteSupplier(Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Equal("Supplier not found.", result.Message);
        }

        [Fact]
        public async Task DeleteSupplier_SupplierExists_ReturnsSuccess()
        {
            var context = CreateContext();

            var supplierId = Guid.NewGuid();
            context.Suppliers.Add(new Supplier
            {
                Id = supplierId,
                Name = "Test Supplier",
                CompanyId = _companyId
            });

            await context.SaveChangesAsync();

            var cacheMock = new Mock<ICustomMemoryCache>();
            var service = CreateService(context, cacheMock);

            var result = await service.DeleteSupplier(supplierId);

            Assert.True(result.Success);
            cacheMock.Verify(
                c => c.RemoveByPrefix(It.IsAny<string>()),
                Times.Once
            );
        }
    }
}
