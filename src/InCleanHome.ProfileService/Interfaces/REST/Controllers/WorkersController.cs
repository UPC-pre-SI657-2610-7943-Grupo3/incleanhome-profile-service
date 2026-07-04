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
/// Worker self-service endpoints (the worker editing his/her own profile).
/// </summary>
/// <remarks>
/// Public-facing worker LISTING and DETAIL endpoints are NOT here — those are
/// served by the Search Service (it composes data from Profile + IAM + Reviews).
/// This controller ONLY exposes /me/* for the logged-in worker editing his/her
/// own basic profile data (10 fields, no enrichment).
/// </remarks>
[ApiController]
[Route("api/v1/workers")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Workers — self-service for the logged-in worker")]
public class WorkersController(
    IWorkerProfileCommandService workerCommandService,
    IWorkerProfileQueryService workerQueryService) : ControllerBase
{
    [HttpGet("me/profile")]
    [SwaggerOperation("Get My Worker Profile",
        "Returns the current worker's basic profile (no enriched fields).")]
    public async Task<IActionResult> GetMyProfile()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return Forbid();

        var profile = await workerQueryService.Handle(new GetWorkerProfileByUserIdQuery(current.UserId));
        if (profile is null) return NotFound(new { error = "Worker profile not found" });

        return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    [HttpPut("me/profile")]
    [SwaggerOperation("Update My Worker Profile", "Updates the current worker's own profile data.")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateWorkerProfileResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return Forbid();

        try
        {
            var profile = await workerCommandService.Handle(new UpdateWorkerProfileCommand(
                current.UserId, body.Name, body.Phone ?? string.Empty,
                body.Age, body.ServiceTypes ?? new(), body.Zones ?? new(),
                body.HourlyRate, body.HourlyRateSunday,
                body.ExperienceYears, body.Bio ?? string.Empty));
            if (profile is null) return NotFound(new { error = "Profile not found" });
            return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPost("me/photo")]
    [SwaggerOperation("Update My Worker Photo",
        "Sets the current worker's profile photo (base64 data URL or public URL).")]
    public async Task<IActionResult> UpdateMyPhoto([FromBody] UpdatePhotoResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return Forbid();

        var profile = await workerCommandService.Handle(
            new UpdateWorkerPhotoCommand(current.UserId, body.PhotoUrl));
        if (profile is null) return NotFound(new { error = "Profile not found" });
        return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }
}
