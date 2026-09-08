using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Software.Api.Controllers;

[ApiController]
[Route("api/v1/software-licenses")]
[Authorize]
public class SoftwareLicensesController : ControllerBase
{
    private readonly ISoftwareLicenseService _licenseService;
    private readonly ICurrentUser _currentUser;

    public SoftwareLicensesController(ISoftwareLicenseService licenseService, ICurrentUser currentUser)
    {
        _licenseService = licenseService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("software.read")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? softwareId = null,
        [FromQuery] LicenseType? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Tenant from the JWT, never from the query string.
        var result = await _licenseService.GetPagedAsync(
            _currentUser.ActiveEntityId, softwareId, type, isActive, page, pageSize, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("software.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpGet("software/{softwareId:guid}")]
    [RequirePermission("software.read")]
    public async Task<IActionResult> GetBySoftwareId(Guid softwareId, CancellationToken ct)
    {
        var result = await _licenseService.GetBySoftwareIdAsync(softwareId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("software.write")]
    public async Task<IActionResult> Create([FromBody] CreateSoftwareLicenseRequest request, CancellationToken ct)
    {
        var result = await _licenseService.CreateAsync(
            request with { EntityNodeId = _currentUser.ActiveEntityId }, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("software.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSoftwareLicenseRequest request, CancellationToken ct)
    {
        var result = await _licenseService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("software.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return NoContent();
    }

    [HttpGet("{id:guid}/compliance")]
    [RequirePermission("software.read")]
    public async Task<IActionResult> CheckCompliance(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.CheckComplianceAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

}
