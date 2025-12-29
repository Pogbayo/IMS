
namespace IMS.Infrastructure.Token
{
    public class JwtSetting
    {
        public string Key { get; set; } = default!;
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public int ExpireHours { get; set; } = 1;
    }
}
