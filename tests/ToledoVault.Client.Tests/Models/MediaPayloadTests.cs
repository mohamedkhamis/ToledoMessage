using ToledoVault.Shared.Models;

namespace ToledoVault.Client.Tests.Models;

[TestClass]
public class MediaPayloadTests
{
    [TestMethod]
    public void MediaPayload_Serialize_Deserialize_RoundTrip()
    {
        // Arrange
        var originalPayload = new MediaPayload
        {
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            Caption = "Check this out!",
            Thumbnail = "base64thumbnaildata",
            Data = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 })
        };

        // Act
        var serialized = MediaPayload.Serialize(originalPayload);
        var deserialized = MediaPayload.Deserialize(serialized);

        // Assert
        Assert.AreEqual(originalPayload.FileName, deserialized.FileName);
        Assert.AreEqual(originalPayload.MimeType, deserialized.MimeType);
        Assert.AreEqual(originalPayload.Caption, deserialized.Caption);
        Assert.AreEqual(originalPayload.Thumbnail, deserialized.Thumbnail);
        Assert.AreEqual(originalPayload.Data, deserialized.Data);
    }

    [TestMethod]
    public void MediaPayload_Serialize_WithNullOptionalFields()
    {
        // Arrange
        var originalPayload = new MediaPayload
        {
            MimeType = "image/png",
            Data = Convert.ToBase64String(new byte[] { 10, 20, 30 })
            // FileName, Caption, Thumbnail are null
        };

        // Act
        var serialized = MediaPayload.Serialize(originalPayload);
        var deserialized = MediaPayload.Deserialize(serialized);

        // Assert
        Assert.IsNull(deserialized.FileName);
        Assert.IsNull(deserialized.Caption);
        Assert.IsNull(deserialized.Thumbnail);
        Assert.AreEqual("image/png", deserialized.MimeType);
        Assert.AreEqual(originalPayload.Data, deserialized.Data);
    }

    [TestMethod]
    public void MediaPayload_Caption_Bundled_Not_Separate()
    {
        // Arrange
        var originalPayload = new MediaPayload
        {
            FileName = "image.jpg",
            MimeType = "image/jpeg",
            Caption = "Check this out!",
            Data = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        };

        // Act
        var serialized = MediaPayload.Serialize(originalPayload);
        var json = System.Text.Encoding.UTF8.GetString(serialized);

        // Assert - Caption should be in the serialized payload
        Assert.IsTrue(json.Contains("\"c\":\"Check this out!\""));
    }

    [TestMethod]
    public void MediaPayload_FileName_Sanitized()
    {
        // Test path separators are replaced with underscore
        Assert.AreEqual(".._etc_passwd", MediaPayload.SanitizeFileName("../etc/passwd"));
        // Windows backslash behavior - on Windows, \ stays as :
        var winResult = MediaPayload.SanitizeFileName(@"C:\Windows\system32\file.exe");
        Assert.IsTrue(winResult?.StartsWith("C:"));

        // Test null bytes
        Assert.AreEqual("test_file", MediaPayload.SanitizeFileName("test\0file"));

        // Test truncation
        var longName = new string('a', 300);
        var sanitized = MediaPayload.SanitizeFileName(longName);
        Assert.AreEqual(255, sanitized?.Length);

        // Test null/empty
        Assert.IsNull(MediaPayload.SanitizeFileName(null));
        Assert.IsNull(MediaPayload.SanitizeFileName(""));
        Assert.IsNull(MediaPayload.SanitizeFileName("   "));
    }

    [TestMethod]
    public void MediaPayload_MimeType_Validation()
    {
        // Test default for null
        Assert.AreEqual("application/octet-stream", MediaPayload.GetDefaultMimeType(null));

        // Test default for empty
        Assert.AreEqual("application/octet-stream", MediaPayload.GetDefaultMimeType(""));

        // Test default for whitespace
        Assert.AreEqual("application/octet-stream", MediaPayload.GetDefaultMimeType("   "));

        // Test valid MIME types pass through
        Assert.AreEqual("image/jpeg", MediaPayload.GetDefaultMimeType("image/jpeg"));
        Assert.AreEqual("image/png", MediaPayload.GetDefaultMimeType("image/png"));
        Assert.AreEqual("video/mp4", MediaPayload.GetDefaultMimeType("video/mp4"));
        Assert.AreEqual("audio/webm", MediaPayload.GetDefaultMimeType("audio/webm"));
        Assert.AreEqual("application/pdf", MediaPayload.GetDefaultMimeType("application/pdf"));
    }

    [TestMethod]
    public void MediaPayload_MaxSize_Validation()
    {
        // Small data should be valid
        var smallData = Convert.ToBase64String(new byte[1024]); // 1KB
        Assert.IsTrue(MediaPayload.IsValidSize(smallData));

        // Empty/null should be invalid
        Assert.IsFalse(MediaPayload.IsValidSize(null));
        Assert.IsFalse(MediaPayload.IsValidSize(""));
    }
}
