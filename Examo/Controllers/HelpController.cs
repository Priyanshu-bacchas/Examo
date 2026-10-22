using Examo.DTOs.Support;
using Examo.Services;

using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

// Help section: FAQ list aur search (login zaroori, global AuthorizeFilter se)
[ApiController]
[Route("api/[controller]")]
public class HelpController : ControllerBase
{
    // GET /api/Help/content  ->  featured cards + saare FAQ
    [HttpGet("content")]
    public IActionResult GetContent()
    {
        return Ok(HelpContent.GetContent());
    }

    // GET /api/Help/search?q=password bhul gaya  ->  milte-julte jawab
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string? q)
    {
        var text = (q ?? string.Empty).Trim();

        if (text.Length > 200)
            text = text[..200];

        var results = HelpContent.Search(text);

        return Ok(new FaqSearchResultDto
        {
            Query = text,
            Found = results.Count > 0,
            Results = results,
            Message = results.Count > 0
                ? string.Empty
                : "We could not find an exact answer. Please try different words."
        });
    }
}