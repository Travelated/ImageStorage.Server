namespace ImageStorage.Server.Amazon;

public class AmazonS3Credentials
{
    public required string HostName { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
}