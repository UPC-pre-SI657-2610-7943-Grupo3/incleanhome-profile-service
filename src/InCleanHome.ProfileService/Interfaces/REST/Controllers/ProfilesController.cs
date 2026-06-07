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
/// <remarks>
/// Endpoints (all prefixed with the gateway's /api/v1/profiles route):
/// <list type="bullet">
///   <item><description>POST  /clients              — create a client profile (called after Auth0/register on IAM)</description></item>
///   <item><description>POST  /workers              — create a worker profile</description></item>
///   <item><description>GET   /me                   — get current user's profile (client OR worker depending on role)</description></item>
///   <item><description>PATCH /me/client            — update current client profile</description></item>
///   <item><description>PATCH /me/worker            — update current worker profile</description></item>
///   <item><description>POST  /me/photo             — set current user's profile photo</description></item>
///   <item><description>GET   /clients/{userId}     — get a client's public profile</description></item>
///   <item><description>GET   /workers/{userId}     — get a worker's public profile</description></item>
///   <item><description>GET   /workers              — list/search workers</description></item>
///   <item><description>POST  /workers/{userId}/completed-service — increment worker stats (called by Reviews/Booking)</description></item>
/// </list>
/// </remarks>
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
    //  Creation (called after IAM has created the User)
    [HttpPost("clients")]
    [SwaggerOperation("Create Client Profile",
        "Creates a client profile. Called by the frontend after a successful /auth/register " +
        "or /auth/auth0/complete-registration on IAM Service.")]
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
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("workers")]
    [SwaggerOperation("Create Worker Profile",
        "Creates a worker profile.")]
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
                body.HourlyRate, body.ExperienceYears, body.Bio ?? string.Empty));
            return CreatedAtAction(
                nameof(GetWorkerByUserId),
                new { userId = profile.UserId },
                WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    //  Current user's profile (/me)
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
        "Updates the current user's client profile. Returns 403 if user is not a client.")]
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
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPatch("me/worker")]
    [SwaggerOperation("Update My Worker Profile",
        "Updates the current user's worker profile. Returns 403 if user is not a worker.")]
    public async Task<IActionResult> UpdateMyWorkerProfile([FromBody] UpdateWorkerProfileResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();
        if (!current.IsWorker()) return Forbid();

        try
        {
            var profile = await workerCommandService.Handle(new UpdateWorkerProfileCommand(
                current.UserId, body.Name, body.Phone ?? string.Empty,
                body.Age,
                body.ServiceTypes ?? new(), body.Zones ?? new(),
                body.HourlyRate, body.ExperienceYears, body.Bio ?? string.Empty));
            if (profile is null) return NotFound(new { error = "Profile not found" });
            return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
        }
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    [HttpPost("me/photo")]
    [SwaggerOperation("Update My Profile Photo",
        "Sets the current user's profile photo. Accepts a base64 data URL or a public URL.")]
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

    //  Public profile reads

    [HttpGet("clients/{userId:int}")]
    [SwaggerOperation("Get Client Profile By UserId",
        "Returns a client's public profile. Used by the chat module to resolve client name and photo.")]
    public async Task<IActionResult> GetClientByUserId(int userId)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var profile = await clientQueryService.Handle(new GetClientProfileByUserIdQuery(userId));
        if (profile is null) return NotFound(new { error = "Client not found" });

        return Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }

    [HttpGet("workers/{userId:int}")]
    [SwaggerOperation("Get Worker Profile By UserId",
        "Returns a worker's public profile.")]
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
        "Note: full search (with availability, distance, etc.) will live in SearchAndCatalog Service.")]
    public async Task<IActionResult> SearchWorkers(
        [FromQuery] string? serviceType,
        [FromQuery] string? zone,
        [FromQuery] string? gender,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery] decimal? maxHourlyRate,
        [FromQuery] decimal? minRating)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        var workers = await workerQueryService.Handle(new SearchWorkersQuery(
            serviceType, zone, gender, minAge, maxAge, maxHourlyRate, minRating));

        return Ok(workers.Select(WorkerResourceFromEntityAssembler.ToResourceFromEntity));
    }

    //  Inter-service hook (e.g. called by Reviews when a service completes)

    public record RegisterCompletedServiceResource(int Rating);

    [HttpPost("workers/{userId:int}/completed-service")]
    [SwaggerOperation("Register Completed Service",
        "Increments the worker's total-services counter and recomputes the running average rating. " +
        "Intended to be called by the Reviews Service (eventually via RabbitMQ events).")]
    public async Task<IActionResult> RegisterCompletedService(int userId, [FromBody] RegisterCompletedServiceResource body)
    {
        var current = (AuthenticatedUser?)HttpContext.Items["User"];
        if (current is null) return Unauthorized();

        // For now only admin can call this directly. In the next iteration this becomes an event consumer.
        if (!current.IsAdmin()) return Forbid();

        var profile = await workerCommandService.Handle(new RegisterWorkerCompletedServiceCommand(userId, body.Rating));
        if (profile is null) return NotFound(new { error = "Worker profile not found" });
        return Ok(WorkerResourceFromEntityAssembler.ToResourceFromEntity(profile));
    }
}
