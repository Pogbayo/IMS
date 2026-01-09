using CloudinaryDotNet.Actions;
using Hangfire;
using IMS.Application.ApiResponse;
using IMS.Application.DTO.Audit;
using IMS.Application.Interfaces;
using IMS.Application.Interfaces.IAudit;
using IMS.Domain.Entities; 
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace IMS.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly IAppDbContext _db;
        private readonly ILogger<AuditService> _logger;
        private readonly ICustomMemoryCache _memoryCache;
        private Dictionary<Guid, CancellationTokenSource> _companyTokens = new();
        public AuditService(ICustomMemoryCache memoryCache,IAppDbContext db, ILogger<AuditService> logger)
        {
            _memoryCache = memoryCache;
            _db = db;
            _logger = logger;
        }

        [Queue("audit")]
        public async Task<Result<List<AuditDto>>> GetAudits(Guid companyId, int pageSize, int pageNumber)
        {
            _logger.LogInformation("Fetching Audits for company");
            // I am adding a key pair value in the _CompanyTokens dictionary here
            // FirstChanceExceptionEventArgs checked if the company id existed in the dictionary before creating a cancellationToken for it and assigning it to the company ID
            if (!_companyTokens.TryGetValue(companyId, out var cts))
            {
                cts = new CancellationTokenSource();
                _companyTokens[companyId] = cts;
            }

            // Defining the Cache Key
            var cacheKey = $"Audit:List:Company:{companyId}:Page:{pageNumber}:Size:{pageSize}";

            List<AuditDto>? cachedAudits = null;
            if (!_memoryCache.TryGetValue<List<AuditDto>>(cacheKey, out cachedAudits))
            {
                if (companyId == Guid.Empty)
                {
                    _logger.LogWarning("Please, provide an ID");
                    return Result<List<AuditDto>>.FailureResponse("Please, provide an ID");
                }

                var company = await _db.Companies.FindAsync(companyId);
                if (company == null)
                    return Result<List<AuditDto>>.FailureResponse("Company does not exist");

                pageNumber = Math.Max(pageNumber, 1);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var audits = await _db.AuditLogs
                    .Where(al => al.CompanyId == companyId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)  
                    .Take(pageSize)
                    .Select(al => new AuditDto
                    {
                        UserId = al.UserId,
                        UserName = al.User!.FirstName,
                        CompanyId = al.CompanyId,
                        Description = al.Description!,
                        Action = al.Action,
                        Timestamp = al.CreatedAt
                    })
                    .ToListAsync();

                cachedAudits = audits;  

                if (audits.Count == 0)
                {
                    _logger.LogWarning("Audit count is 0");
                }

                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    // The moment the Token gets cancelled, this sends a signal to the cache system to invalidate the cached record for the company
                    ExpirationTokens = { new CancellationChangeToken(cts.Token) }
                };

                // Cache configuration...
                _memoryCache.Set(cacheKey, cachedAudits, options);
            }

            return Result<List<AuditDto>>.SuccessResponse(cachedAudits ?? new List<AuditDto>(),
                cachedAudits?.Count > 0 ? "Audits retrieved successfully..." : "No audits found for the specified criteria.");
        }
        public async Task LogAsync(Guid userId, Guid companyId, AuditAction action , string description)
        {
            var log = new AuditLog
            {
                UserId = userId,
                CompanyId = companyId,
                Action = action,
                Description = description
            };

            await _db.AuditLogs.AddAsync(log);
            await _db.SaveChangesAsync();

            if (_companyTokens.TryGetValue(companyId,out var cts))
            {
                if (cts == null)
                {
                    _logger.LogWarning("Company found in the dictionary but token was null");
                    return;
                }

                cts.Cancel();
                cts.Dispose();
                _companyTokens.Remove(companyId);
            }
            else
            {
                _logger.LogWarning("Company not found in the dictionary..");
            }
        }
    }
}
