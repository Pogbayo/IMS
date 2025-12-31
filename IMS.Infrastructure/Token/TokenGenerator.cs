using IMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IMS.Infrastructure.Token
{
    public class TokenGenerator : ITokenGenerator
    {
        private readonly JwtSetting _jwtSettings;
        private readonly ILogger<TokenGenerator> _logger;
        private readonly UserManager<AppUser> _userManager;

        public TokenGenerator(IOptions<JwtSetting> jwtOptions,UserManager<AppUser> userManager, ILogger<TokenGenerator> logger)
        {
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
            _userManager = userManager;
        }

      
       public async Task<string> GenerateAccessToken(AppUser user)
        {
            if (user == null)
            {
                _logger.LogWarning("User object is null");
                return "User object is null";
            }

            var claims = new List<Claim>
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var expiryMinutes = _jwtSettings.ExpireHours * 60;
            SymmetricSecurityKey symmetricSecurityKey = new(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var key = symmetricSecurityKey;
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                expires: DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).UtcDateTime,
                claims: claims,
                signingCredentials: creds);
            //_logger.LogInformation($"This is your token :{token}");

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
