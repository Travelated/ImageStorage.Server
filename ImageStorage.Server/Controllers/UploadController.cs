using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Azure.Storage.Blobs;
using Imageflow.Fluent;
using ImageStorage.Server.Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ImageStorage.Server.Controllers;

public record ImageDimensions
{
    public required long Width { get; init; }
    public required long Height { get; init; }
}

public interface IUploadBlobStorage
{
    Task<Stream?> OpenReadStream(string bucket, string fileName, CancellationToken cancellationToken);
    Task UploadStream(string bucket, string fileName, Stream stream, CancellationToken cancellationToken);
}


[ApiController]
[Route("api")]
public class UploadController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    
    public UploadController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    [HttpGet("jwt-debug")]
    public IActionResult GetJwtDebug(string fileName)
    {
        var config = _serviceProvider.GetRequiredService<AzureUploadConfig>();
        if (!config.AllowDebug)
        {
            return NotFound();
        }
        
        var claims = new Dictionary<string, string>
        {
            {"fileName", fileName},
            // Add other claims as needed
        };

        string token = GenerateToken(config.JwtKey, claims, DateTime.UtcNow + TimeSpan.FromDays(1));
        return Ok(token);
    }

    string? AuthorizeAndGetFileName()
    {
        var config = _serviceProvider.GetRequiredService<AzureUploadConfig>();
        
        // 2. Extract the access token from the header
        var accessToken = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        // Remove the "Bearer " part from the token string
        accessToken = accessToken.ToString().Replace("Bearer ", "");

        // 3. Verify the JWT token 
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(config.JwtKey); // This is your method to get the parameters needed for validation

        SecurityToken validatedToken;
        try
        {
            tokenHandler.ValidateToken(accessToken, validationParameters, out validatedToken);
        }
        catch
        {
            return null;
        }

        // Extract uploaded file name from JWT payload
        var jwtToken = validatedToken as JwtSecurityToken;
        if (jwtToken == null)
        {
            return null;
        }
        var uploadedFileName = jwtToken.Claims.First(c => c.Type == "fileName").Value;

        return uploadedFileName;
    }
    
    [RequestSizeLimit(50_000_000)]
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ImageDimensions), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImageDimensions>> UploadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var uploadedFileName = AuthorizeAndGetFileName();
        if (uploadedFileName == null)
        {
            return Unauthorized();
        }
        
        var config = _serviceProvider.GetRequiredService<AzureUploadConfig>();

        // 4. Upload to Azure Blob Storage
        IUploadBlobStorage blobServiceClient = _serviceProvider.GetRequiredService<IUploadBlobStorage>();
        
        var blobClient = await blobServiceClient.OpenReadStream(config.Container, uploadedFileName, cancellationToken);

        await using var fileStream = file.OpenReadStream();
        using var memoryStream = await StreamToMemoryStreamAsync(fileStream);

        // Check if the file already exists
        if (blobClient != null)
        {
            // File exists, now compare
            using var existingFileStream = new MemoryStream();
            await blobClient.CopyToAsync(existingFileStream, cancellationToken);
            existingFileStream.Position = 0; // Reset the stream position for reading
            
            if (!StreamsAreEqual(existingFileStream, memoryStream)) 
            {
                return Conflict("File already exists and it's different from the uploaded file.");
            }
            return Ok();
        }
        memoryStream.Position = 0;
        
        var info = await ImageJob.GetImageInfo(new StreamSource(memoryStream, false), cancellationToken);
        if (info.ImageHeight <= config.MaxSize && info.ImageWidth <= config.MaxSize && info.PreferredMimeType == "image/webp")
        {
            // No need to resize, just upload
            await blobServiceClient.UploadStream(config.Container, uploadedFileName, memoryStream, cancellationToken);
            return new ImageDimensions
            {
                Width = info.ImageWidth,
                Height = info.ImageHeight
            };
        }
        
        memoryStream.Position = 0;
        
        // Resize the image to webp
        using var imageJob = new ImageJob();
        var jobResult = await imageJob.Decode(memoryStream, true)
            .Constrain(new Constraint(config.MaxSize, config.MaxSize)
            {
                Mode = ConstraintMode.Within
            })
            .EncodeToBytes(new WebPLossyEncoder(80))
            .Finish().InProcessAsync();

        if (jobResult != null)
        {
            var resizedImage = jobResult.First; 
            var dimensions = new ImageDimensions
            {
                Width = resizedImage.Width,
                Height = resizedImage.Height
            };
            
            var bytes = resizedImage.TryGetBytes();
            if (bytes?.Array != null)
            {
                using MemoryStream resizedImageStream = new MemoryStream(bytes.Value.Array, 
                    bytes.Value.Offset, 
                    bytes.Value.Count);
                
                resizedImageStream.Position = 0;
                await blobServiceClient.UploadStream(config.Container, uploadedFileName, resizedImageStream, cancellationToken);
                return dimensions;
            }
        }
        
        throw new Exception("Failed to resize image");
    }
    
    [HttpGet("get-size")]
    [ProducesResponseType(typeof(ImageDimensions), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImageDimensions>> GetImageSizeAsync(CancellationToken cancellationToken)
    {
        var imageName = AuthorizeAndGetFileName();

        if (imageName == null)
        {
            return Unauthorized();
        }

        var config = _serviceProvider.GetRequiredService<AzureUploadConfig>();
        IUploadBlobStorage blobServiceClient = _serviceProvider.GetRequiredService<IUploadBlobStorage>();
        
        
        var file = await blobServiceClient.OpenReadStream(config.Container, imageName, cancellationToken);

        if (file == null)
        {
            return NotFound(); // Return 404 if the image doesn't exist
        }

        // Download the blob to get its dimensions
        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var info = await ImageJob.GetImageInfo(new StreamSource(memoryStream, false), cancellationToken);
        var dimensions = new ImageDimensions
        {
            Width = info.ImageWidth,
            Height = info.ImageHeight
        };

        return Ok(dimensions);
    }


    private string GenerateToken(string key, IDictionary<string, string> claims, DateTime expireDate)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    
        var tokenClaims = claims.Select(claim => new Claim(claim.Key, claim.Value)).ToArray();

        var token = new JwtSecurityToken(
            claims: tokenClaims,
            expires: expireDate,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    private TokenValidationParameters GetValidationParameters(string key)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

            // Set the clock skew to zero, in case you want the tokens to expire exactly at token expiration time 
            ClockSkew = TimeSpan.Zero
        };
    }
    
    private bool StreamsAreEqual(Stream firstStream, Stream secondStream)
    {
        const int bufferSize = 1024 * sizeof(long);
        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true)
        {
            int count1 = firstStream.Read(buffer1, 0, bufferSize);
            int count2 = secondStream.Read(buffer2, 0, bufferSize);

            if (count1 != count2)
                return false;

            if (count1 == 0)
                return true;

            // You might replace the following with more efficient comparison code depending on your needs
            for (int i = 0; i < count1; i++)
            {
                if (buffer1[i] != buffer2[i])
                    return false;
            }
        }
    }
    
    async Task<MemoryStream> StreamToMemoryStreamAsync(Stream input)
    {
        MemoryStream memoryStream = new MemoryStream();

        // Buffer to hold the stream data.
        byte[] buffer = new byte[40960]; // You can adjust the buffer size if needed

        int bytesRead;
        while((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead);
        }

        // Reset the position of the memory stream to be at the start of the stream.
        memoryStream.Position = 0;

        return memoryStream;
    }
    
    async Task<ImageDimensions> GetImageDimensions(Stream imageStream)
    {
        var info = await ImageJob.GetImageInfo(new StreamSource(imageStream, false));
        return new ImageDimensions
        {
            Width = info.ImageWidth,
            Height = info.ImageHeight
        };
    }
}