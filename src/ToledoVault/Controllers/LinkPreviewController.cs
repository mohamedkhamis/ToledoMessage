using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToledoVault.Services;

namespace ToledoVault.Controllers;

[ApiController]
[Route("api/link-preview")]
[Authorize]
public class LinkPreviewController(LinkPreviewService linkPreviewService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("URL is required.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest("Invalid URL.");

        var preview = await linkPreviewService.GetPreviewAsync(url);
        if (preview is null)
            return NoContent();

        return Ok(preview);
    }
}
