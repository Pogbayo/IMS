namespace IMS.Infrastructure.CloudWatch
{
    public class AwsSettings
    {
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string LogGroupName { get; set; } = string.Empty;
        public string LogStreamName { get; set; } = string.Empty;
    }
}
