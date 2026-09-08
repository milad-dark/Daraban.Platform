using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService) => _locationService = locationService;

    [HttpGet]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _locationService.GetAllAsync(ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _locationService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Create([FromBody] CreateLocationRequest request, CancellationToken ct)
    {
        var result = await _locationService.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateLocationRequest request, CancellationToken ct)
    {
        var result = await _locationService.UpdateAsync(id, request, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("assets.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _locationService.DeleteAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return NoContent();
    }

}
