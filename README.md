# Image Resize and Storage Server by [Travelated](https://www.travelated.com/)

Welcome to the Image Resize and Storage Server built on top of Imageflow .NET! This server solution provides efficient and secure image processing capabilities suitable for a wide range of applications, including modern web services and content management systems.

## Features

1. **Image Uploads with JWT Security**:
    - Seamlessly upload images using JWT tokens from your client's code.
    - Automatically resize images upon upload.
    - Store resized images in Azure Blob Storage.
    - Get back the original image dimensions.
    - Configuration example:
      ```json
      "AzureUpload": {
          "Enabled": true,
          "Container": "static-images",
          "JwtKey": "YOUR_JWT_KEY_HERE"
      }
      ```
    - Endpoint: `/api/upload`

2. **Serving Images from Azure Container**:
    - Serve your images directly from Azure Blob storage.
    - Integrate easily with `builder.Services.AddImageflowAzureBlobService` to mount your Azure container.

3. **SEO Tools**:
    - An auto-generated `robots.txt` to ensure web crawlers can index your images.
    - Redirect functionality from `/` to a URL of your choice.

4. **Next.js Image Streaming Integration**:
    - If you use Next.js or similar platforms, leverage the Image Resize and Storage Server for efficient image loading and resizing.
    - The server can fetch and resize images from third-party domains on-the-fly.
    - Configure with `builder.Services.AddImageflowRemoteReaderService`.

5. **Imageflow Azure Blob Cache**:
    - Optimize performance by caching resized images in Azure Blob storage.
    - Implement using: `services.AddSingleton<IStreamCache, AzureBlobCache>`.

## Getting Started

1. **Installation**:
    - Clone the repository.
    - Install the required packages and dependencies.

2. **Configuration**:
    - Update the app settings with your Azure Blob Storage details, JWT key, and other configurations as mentioned above.

3. **Run**:
    - Start the server and access the API endpoints as per your requirements.

## Contributing

Feel free to fork, raise issues, and submit Pull Requests. We appreciate community contributions to keep this server solution up-to-date and robust.

