using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ImageStorage.Server.Extensions;

public class SwaggerFileOperationFilter : IOperationFilter  
{  
    public void Apply(OpenApiOperation operation, OperationFilterContext context)  
    {  
        var fileUploadMime = "multipart/form-data";  
        if (operation.RequestBody == null || !operation.RequestBody.Content.Any(x => x.Key.Equals(fileUploadMime, StringComparison.InvariantCultureIgnoreCase)))  
            return;  
  
        var fileParams = context.MethodInfo.GetParameters().Where(p => p.ParameterType == typeof(IFormFile));  
        operation.RequestBody.Content[fileUploadMime].Schema.Properties =  
#pragma warning disable CS8714
            fileParams.ToDictionary(k => k.Name ?? throw new Exception($"Empty value {k.Name}"), v => new OpenApiSchema()  
#pragma warning restore CS8714
            {  
                Type = "string",  
                Format = "binary"  
            });  
    }  
}
