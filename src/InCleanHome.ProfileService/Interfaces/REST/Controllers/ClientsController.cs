using System.Net.Mime;
using InCleanHome.ProfileService.Domain.Model.Commands;
using InCleanHome.ProfileService.Domain.Model.Queries;
using InCleanHome.ProfileService.Domain.Services;
using InCleanHome.ProfileService.Infrastructure.Pipeline;
using InCleanHome.ProfileService.Interfaces.REST.Resources;
using InCleanHome.ProfileService.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace InCleanHome.ProfileService.Interfaces.REST.Controllers;

/// <summary>
/// Public-facing endpoints for client profiles (matches frontend URLs).
/// </summary>
[ApiController]
[Route("api/v1/clients")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Clients — public client profile endpoints")]
public class ClientsController(
    IClientProfileCommandService clientCommandService,
    IClientProfileQueryService clientQueryService) : ControllerBase
{
    // ── /clients/my-profile ─────────────────────────────────────────────────

    [HttpGet("my-profile")]
    [SwaggerOperation("Get My Client Profile", "Returns the current client's own profile.")]
    public async Task<IActionResult> GetMyProfile()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        var profile = await clientQueryService.Handle(new GetClientProfileByUserIdQuery(current.UserId));
        if (profile is null) return NotFound(new { error = "Client profile not found" });

        return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    [HttpPatch("my-profile")]
    [SwaggerOperation("Update My Client Profile", "Updates the current client's own profile.")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateClientProfileResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        try
        {
            var profile = await clientCommandService.Handle(
                new UpdateClientProfileCommand(current.UserId, body.Name, body.Phone ?? string.Empty));
            if (profile is null) return NotFound(new { error = "Profile not found" });
            return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPost("my-profile/photo")]
    [SwaggerOperation("Update My Client Photo",
        "Sets the current client's profile photo (base64 data URL or public URL).")]
    public async Task<IActionResult> UpdateMyPhoto([FromBody] UpdatePhotoResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsClient()) return Forbid();

        var profile = await clientCommandService.Handle(
            new UpdateClientPhotoCommand(current.UserId, body.PhotoUrl));
        if (profile is null) return NotFound(new { error = "Profile not found" });
        return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    // ── /clients/{userId} (public lookup) ───────────────────────────────────

    [HttpGet("{userId:int}")]
    [SwaggerOperation("Get Client Profile By UserId",
        "Returns a client's public profile (used by chat to resolve names).")]
    public async Task<IActionResult> GetClientByUserId(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var profile = await clientQueryService.Handle(new GetClientProfileByUserIdQuery(userId));
        if (profile is null) return NotFound(new { error = "Client not found" });

        return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }
}
