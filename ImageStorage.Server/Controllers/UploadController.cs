using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Azure.Storage.Blobs;
using ImageStorage.Server.Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ImageStorage.Server.Controllers;

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
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFileAsync(IFormFile file)
    {
        var config = _serviceProvider.GetRequiredService<AzureUploadConfig>();
        
        // 2. Extract the access token from the header
        var accessToken = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(accessToken))
        {
            return Unauthorized();
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
            return Unauthorized();
        }

        // Extract uploaded file name from JWT payload
        var jwtToken = validatedToken as JwtSecurityToken;
        if (jwtToken == null)
        {
            return Unauthorized();
        }
        var uploadedFileName = jwtToken.Claims.First(c => c.Type == "fileName").Value;

        // 4. Upload to Azure Blob Storage
        BlobServiceClient blobServiceClient = _serviceProvider.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(config.Container);
    
        var blobClient = containerClient.GetBlobClient(uploadedFileName);
    
        // Check if the file already exists
        if (await blobClient.ExistsAsync())
        {
            // File exists, now compare
            using var existingFileStream = new MemoryStream();
            await blobClient.DownloadToAsync(existingFileStream);
            existingFileStream.Position = 0; // Reset the stream position for reading

            using var newFileStream = file.OpenReadStream();
            if (!StreamsAreEqual(existingFileStream, newFileStream)) 
            {
                return Conflict("File already exists and it's different from the uploaded file.");
            }
            return Ok();
        }
    
        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, true);
    
        return Ok();
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
}