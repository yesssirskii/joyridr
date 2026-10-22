using System.ComponentModel.DataAnnotations;
using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Models.Enums;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("api/cars")]
[Authorize]
public class CarsController(CarService cars) : ApiControllerBase
{
    /// <summary>Listed cars whose driver is based within radiusKm of the given point, closest first.</summary>
    [HttpGet("nearby")]
    public Task<PagedResult<NearbyCarDto>> Nearby(
        [Range(-90, 90)] double latitude,
        [Range(-180, 180)] double longitude,
        CancellationToken ct,
        double radiusKm = 25,
        CarCategory? category = null,
        int? minSeats = null,
        decimal? maxPricePerHour = null,
        int page = 1,
        int pageSize = 20) =>
        cars.SearchNearbyAsync(new NearbyCarsQuery(latitude, longitude, radiusKm, category, minSeats, maxPricePerHour, page, pageSize), ct);

    /// <summary>Car details. Unlisted cars are only visible to their owner and admins.</summary>
    [HttpGet("{carId:guid}")]
    public Task<CarDetailDto> Get(Guid carId, CancellationToken ct) => cars.GetAsync(carId, CurrentUserId, CurrentUserRole, ct);

    /// <summary>Add a car. It stays hidden until an admin approves it.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Driver)]
    public async Task<ActionResult<CarDetailDto>> Create(UpsertCarRequest request, CancellationToken ct)
    {
        var car = await cars.CreateAsync(CurrentUserId, request, ct);
        return CreatedAtAction(nameof(Get), new { carId = car.Id }, car);
    }

    /// <summary>Edit a car. Changing make, model, year or category sends it back for review.</summary>
    [HttpPut("{carId:guid}")]
    [Authorize(Policy = Policies.Driver)]
    public Task<CarDetailDto> Update(Guid carId, UpsertCarRequest request, CancellationToken ct) =>
        cars.UpdateAsync(CurrentUserId, carId, request, ct);

    /// <summary>Remove a car (not possible while it has upcoming rides).</summary>
    [HttpDelete("{carId:guid}")]
    [Authorize(Policy = Policies.Driver)]
    public async Task<IActionResult> Delete(Guid carId, CancellationToken ct)
    {
        await cars.DeleteAsync(CurrentUserId, carId, ct);
        return NoContent();
    }

    /// <summary>Upload a car photo (multipart/form-data, field name "file"). The first photo is the cover.</summary>
    [HttpPost("{carId:guid}/photos")]
    [Authorize(Policy = Policies.Driver)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CarPhotoDto>> AddPhoto(Guid carId, IFormFile file, CancellationToken ct)
    {
        var photo = await cars.AddPhotoAsync(CurrentUserId, carId, file, ct);
        return Created(photo.Url, photo);
    }

    /// <summary>Delete a car photo.</summary>
    [HttpDelete("{carId:guid}/photos/{photoId:guid}")]
    [Authorize(Policy = Policies.Driver)]
    public async Task<IActionResult> DeletePhoto(Guid carId, Guid photoId, CancellationToken ct)
    {
        await cars.DeletePhotoAsync(CurrentUserId, carId, photoId, ct);
        return NoContent();
    }
}
