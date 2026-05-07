using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace VNFootballLeagues.Services.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly bool _enabled;

    public CloudinaryService(IConfiguration config)
    {
        var cloudName = config["CloudinarySettings:CloudName"];
        var apiKey    = config["CloudinarySettings:ApiKey"];
        var apiSecret = config["CloudinarySettings:ApiSecret"];

        _enabled = !string.IsNullOrWhiteSpace(cloudName) && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret);

        if (_enabled)
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }
    }

    /// <summary>Upload ảnh lên Cloudinary, trả về URL. Nếu chưa config thì trả về null.</summary>
    public async Task<string?> UploadAvatarAsync(Stream fileStream, string fileName, string userId)
    {
        if (!_enabled || _cloudinary == null) return null;

        var publicId = $"avatars/{userId}";
        var uploadParams = new ImageUploadParams
        {
            File           = new FileDescription(fileName, fileStream),
            PublicId       = publicId,
            Overwrite      = true,
            Transformation = new Transformation().Width(400).Height(400).Crop("fill").Gravity("face"),
            Folder         = "vnfootball"
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        return result.SecureUrl?.ToString();
    }

    /// <summary>Upload ảnh hỗ trợ (support chat) lên Cloudinary.</summary>
    public async Task<string?> UploadSupportImageAsync(Stream fileStream, string fileName, string ticketId)
    {
        if (!_enabled || _cloudinary == null) return null;

        var publicId = $"support/{ticketId}/{Guid.NewGuid()}";
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            PublicId = publicId,
            Overwrite = false,
            Folder = "vnfootball"
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        return result.SecureUrl?.ToString();
    }

    /// <summary>Upload ảnh Sofascore lên Cloudinary với public_id cố định để cache.</summary>
    public async Task<string?> UploadSofascoreImageAsync(byte[] imageBytes, string contentType, string publicId)
    {
        if (!_enabled || _cloudinary == null) return null;

        using var stream = new MemoryStream(imageBytes);
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription("img", stream),
            PublicId = $"vnfootball/sofascore/{publicId}",
            Overwrite = true,
            Invalidate = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
            throw new Exception($"Cloudinary upload error: {result.Error.Message}");
        return result.SecureUrl?.ToString();
    }

    /// <summary>Build URL Cloudinary từ publicId mà không cần gọi API. Trả về null nếu chưa config.</summary>
    public string? GetCachedUrl(string publicId)
    {
        if (!_enabled || _cloudinary == null) return null;

        // Build Cloudinary URL qua SDK — tự xử lý encoding đúng chuẩn
        return _cloudinary.Api.UrlImgUp.BuildUrl($"vnfootball/sofascore/{publicId}");
    }

    public bool IsEnabled => _enabled;
}
