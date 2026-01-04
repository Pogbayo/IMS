using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using IMS.Application.Interfaces;
using IMS.Infrastructure.CloudWatch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class CloudWatchLogger : ICloudWatchLogger
{
    // This is the AWS client that actually talks to CloudWatch
    private readonly AmazonCloudWatchLogsClient _client;

    // The log group is basically a folder for logs. 
    // We put all our app audit logs here
    private readonly string _logGroupName;

    // A log stream is like a file inside the folder
    // Each stream is a sequence of events from one source
    private readonly string _logStreamName;

    // Just in case something goes wrong, or I want to debug locally
    // Can log warnings/errors in the console or dev logs
    private readonly ILogger<CloudWatchLogger> _logger;

    // Keeps track of where we left off in the stream
    // AWS needs this to make sure logs stay in order
    private string _sequenceToken = "";

    // Constructor gets settings from DI and sets up the AWS client
    public CloudWatchLogger(IOptions<AwsSettings> awsSettings, ILogger<CloudWatchLogger> logger)
    {
        _logger = logger;
        var settings = awsSettings.Value;

        _logGroupName = settings.LogGroupName;
        _logStreamName = settings.LogStreamName;

        // Create the AWS client with our credentials and region
        _client = new AmazonCloudWatchLogsClient(
            settings.AccessKeyId,
            settings.SecretAccessKey,
            RegionEndpoint.GetBySystemName(settings.Region)
        );

        // Make sure the log group and stream exist in AWS
        // I have to block here with .Wait() because constructors can't be async
        EnsureLogGroupAndStreamExist().Wait();
    }

    // This just checks if the log group and log stream exist
    // If they don’t, create them. If they already exist, grab the sequence token
    private async Task EnsureLogGroupAndStreamExist()
    {
        try
        {
            // Try to make the folder (log group)
            await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _logGroupName });
        }
        catch
        {
            // If it already exists, AWS will throw — ignore that
        }

        try
        {
            // Try to make the file (log stream)
            await _client.CreateLogStreamAsync(new CreateLogStreamRequest
            {
                LogGroupName = _logGroupName,
                LogStreamName = _logStreamName
            });
        }
        catch
        {
            // If the stream already exists, we need the last sequence token
            // This is important so we can append new logs in order
            var describe = await _client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamNamePrefix = _logStreamName
            });

            // Save the sequence token for next log
            _sequenceToken = describe.LogStreams.First().UploadSequenceToken;
        }
    }

    // The main method to log stuff to CloudWatch
    public async Task LogAsync(string message)
    {
        // Make a log event with message + current time
        var logEvent = new InputLogEvent
        {
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        // Prepare request to send to AWS
        var request = new PutLogEventsRequest
        {
            LogGroupName = _logGroupName,
            LogStreamName = _logStreamName,
            LogEvents = new List<InputLogEvent> { logEvent },
            SequenceToken = _sequenceToken // must provide token to keep order
        };

        try
        {
            // Actually send it
            var response = await _client.PutLogEventsAsync(request);

            // Save next token for future logs
            _sequenceToken = response.NextSequenceToken;
        }
        catch (InvalidSequenceTokenException ex)
        {
            // If AWS complains that our token is wrong (maybe someone else wrote to the stream)
            // we grab the latest token and try again
            var describe = await _client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamNamePrefix = _logStreamName
            });

            _sequenceToken = describe.LogStreams.First().UploadSequenceToken;

            // Retry with the updated token
            request.SequenceToken = _sequenceToken;
            var response = await _client.PutLogEventsAsync(request);
            _sequenceToken = response.NextSequenceToken;

            // Log the original exception in case we need to debug
            _logger.LogWarning(ex.ToString());
        }
    }
}
