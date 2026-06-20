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
/// Profile management endpoints for clients and workers.
/// </summary>
[ApiController]
[Route("api/v1/profiles")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Profiles — client/worker profile management")]
public class ProfilesController(
    IClientProfileCommandService clientCommandService,
    IClientProfileQueryService clientQueryService,
    IWorkerProfileCommandService workerCommandService,
    IWorkerProfileQueryService workerQueryService) : ControllerBase
{

    [HttpPost("clients")]
    [SwaggerOperation("Create Client Profile",
        "Creates a client profile. Called by IAM Service after creating the User.")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientProfileResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        // A user can only create their own profile (unless admin).
        if (current.UserId != body.UserId && !current.IsAdmin())
            return Forbid();

        try
        {
            var profile = await clientCommandService.Handle(
                new CreateClientProfileCommand(body.UserId, body.Name, body.Phone ?? string.Empty));
            return CreatedAtAction(
                nameof(GetClientByUserId),
                new { userId = profile.UserId },
                ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpPost("workers")]
    [SwaggerOperation("Create Worker Profile", "Creates a worker profile.")]
    public async Task<IActionResult> CreateWorker([FromBody] CreateWorkerProfileResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        if (current.UserId != body.UserId && !current.IsAdmin())
            return Forbid();

        try
        {
            var profile = await workerCommandService.Handle(new CreateWorkerProfileCommand(
                body.UserId, body.Name, body.Phone ?? string.Empty,
                body.Age, body.Gender,
                body.ServiceTypes ?? new(), body.Zones ?? new(),
                body.HourlyRate, body.HourlyRateSunday,
                body.ExperienceYears, body.Bio ?? string.Empty));
            return CreatedAtAction(
                nameof(GetWorkerByUserId),
                new { userId = profile.UserId },
                WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e) { return BadRequest(new { error = e.Message }); }
    }


    [HttpGet("me")]
    [SwaggerOperation("Get My Profile",
        "Returns the current user's profile. Picks client or worker based on JWT role claim.")]
    public async Task<IActionResult> GetMyProfile()
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        if (current.IsClient())
        {
            var profile = await clientQueryService.Handle(new GetClientProfileByUserIdQuery(current.UserId));
            if (profile is null) return NotFound(new { error = "Client profile not found" });
            return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }

        if (current.IsWorker())
        {
            var profile = await workerQueryService.Handle(new GetWorkerProfileByUserIdQuery(current.UserId));
            if (profile is null) return NotFound(new { error = "Worker profile not found" });
            return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }

        return BadRequest(new { error = "Profiles are not managed for this role" });
    }

    [HttpPatch("me/client")]
    [SwaggerOperation("Update My Client Profile",
        "Updates the current user's client profile. Returns 403 if not a client.")]
    public async Task<IActionResult> UpdateMyClientProfile([FromBody] UpdateClientProfileResource body)
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

    [HttpPatch("me/worker")]
    [SwaggerOperation("Update My Worker Profile",
        "Updates the current user's worker profile. Returns 403 if not a worker.")]
    public async Task<IActionResult> UpdateMyWorkerProfile([FromBody] UpdateWorkerProfileResource body)
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
    [SwaggerOperation("Update My Profile Photo",
        "Sets the current user's profile photo (base64 data URL or public URL).")]
    public async Task<IActionResult> UpdateMyPhoto([FromBody] UpdatePhotoResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        if (current.IsClient())
        {
            var profile = await clientCommandService.Handle(
                new UpdateClientPhotoCommand(current.UserId, body.PhotoUrl));
            if (profile is null) return NotFound(new { error = "Profile not found" });
            return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }

        if (current.IsWorker())
        {
            var profile = await workerCommandService.Handle(
                new UpdateWorkerPhotoCommand(current.UserId, body.PhotoUrl));
            if (profile is null) return NotFound(new { error = "Profile not found" });
            return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }

        return BadRequest(new { error = "Profiles are not managed for this role" });
    }


    [HttpGet("clients/{userId:int}")]
    [SwaggerOperation("Get Client Profile By UserId",
        "Returns a client's public profile (used by Twilio chat for resolving names).")]
    public async Task<IActionResult> GetClientByUserId(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var profile = await clientQueryService.Handle(new GetClientProfileByUserIdQuery(userId));
        if (profile is null) return NotFound(new { error = "Client not found" });

        return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    [HttpGet("workers/{userId:int}")]
    [SwaggerOperation("Get Worker Profile By UserId", "Returns a worker's public profile.")]
    public async Task<IActionResult> GetWorkerByUserId(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var profile = await workerQueryService.Handle(new GetWorkerProfileByUserIdQuery(userId));
        if (profile is null) return NotFound(new { error = "Worker not found" });

        return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    [HttpGet("workers")]
    [SwaggerOperation("List/Search Workers",
        "Returns workers, optionally filtered by service type, zone, gender, age, hourly rate, or rating. " +
        "If `serviceTypes` (CSV) is set, the worker must offer ALL listed services (AND).")]
    public async Task<IActionResult> SearchWorkers(
        [FromQuery] string? serviceType,
        [FromQuery] string? serviceTypes,
        [FromQuery] string? zone,
        [FromQuery] string? gender,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery] decimal? maxHourlyRate,
        [FromQuery] decimal? minRating)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var serviceTypesList = !string.IsNullOrWhiteSpace(serviceTypes)
            ? serviceTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : null;

        var workers = await workerQueryService.Handle(new SearchWorkersQuery(
            serviceType, zone, gender, minAge, maxAge, maxHourlyRate, minRating, serviceTypesList));

        return Ok(workers.Select(WorkerResourceFromEntityAssembler.ToResourceFromEntity));
    }
}
