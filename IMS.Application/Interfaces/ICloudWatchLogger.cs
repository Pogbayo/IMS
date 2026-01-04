

namespace IMS.Application.Interfaces
{
    public interface ICloudWatchLogger
    {
        Task LogAsync(string message);
    }

}
