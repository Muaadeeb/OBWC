namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/stores")]
public class StoresController(ApplicationDbContext db) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StoreDto>>> List(CancellationToken cancellationToken) =>
        Ok(await db.GameStores.AsNoTracking().OrderBy(store => store.SortOrder).ThenBy(store => store.Name)
            .Select(store => new StoreDto
            {
                Id = store.Id,
                Name = store.Name,
                FormerName = store.FormerName,
                Address = store.Address,
                Description = store.Description,
                Bullet1 = store.Bullet1,
                Bullet2 = store.Bullet2,
                Bullet3 = store.Bullet3,
                SortOrder = store.SortOrder
            })
            .ToListAsync(cancellationToken));

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<StoreDto>> Create(SaveStoreRequest request, CancellationToken cancellationToken)
    {
        var store = new GameStore { Id = Guid.NewGuid() };
        Apply(store, request);
        db.GameStores.Add(store);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/stores/{store.Id}", Map(store));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<StoreDto>> Update(Guid id, SaveStoreRequest request, CancellationToken cancellationToken)
    {
        var store = await db.GameStores.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (store is null)
            return NotFound();
        Apply(store, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(store));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var store = await db.GameStores.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (store is null)
            return NotFound();
        db.GameStores.Remove(store);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(GameStore store, SaveStoreRequest request)
    {
        store.Name = request.Name.Trim();
        store.FormerName = Clean(request.FormerName);
        store.Address = Clean(request.Address);
        store.Description = Clean(request.Description);
        store.Bullet1 = Clean(request.Bullet1);
        store.Bullet2 = Clean(request.Bullet2);
        store.Bullet3 = Clean(request.Bullet3);
        store.SortOrder = request.SortOrder;
    }

    private static StoreDto Map(GameStore store) => new()
    {
        Id = store.Id,
        Name = store.Name,
        FormerName = store.FormerName,
        Address = store.Address,
        Description = store.Description,
        Bullet1 = store.Bullet1,
        Bullet2 = store.Bullet2,
        Bullet3 = store.Bullet3,
        SortOrder = store.SortOrder
    };
}

[ApiController]
[Route("api/groups")]
public class GroupsController(ApplicationDbContext db) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> List(CancellationToken cancellationToken)
    {
        var groups = await db.GamingGroups.AsNoTracking()
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken);
        return Ok(groups.Select(Map).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<GroupDto>> Create(SaveGroupRequest request, CancellationToken cancellationToken)
    {
        var group = new GamingGroup { Id = Guid.NewGuid() };
        Apply(group, request);
        db.GamingGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/groups/{group.Id}", Map(group));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<GroupDto>> Update(Guid id, SaveGroupRequest request, CancellationToken cancellationToken)
    {
        var group = await db.GamingGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
            return NotFound();
        Apply(group, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(group));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var group = await db.GamingGroups.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
            return NotFound();
        db.GamingGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(GamingGroup group, SaveGroupRequest request)
    {
        group.Name = request.Name.Trim();
        group.Description = Clean(request.Description);
        group.GameFocus = Clean(request.GameFocus);
        group.WebsiteUrl = Clean(request.WebsiteUrl);
        group.Contact = Clean(request.Contact);
        group.SortOrder = request.SortOrder;
    }

    private static GroupDto Map(GamingGroup group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        Description = group.Description,
        GameFocus = group.GameFocus,
        WebsiteUrl = group.WebsiteUrl,
        Contact = group.Contact,
        SortOrder = group.SortOrder
    };
}

[ApiController]
[Route("api/conventions")]
public class ConventionsController(ApplicationDbContext db) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ConventionDto>>> List(CancellationToken cancellationToken)
    {
        var conventions = await db.Conventions.AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return Ok(conventions.Select(Map).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<ConventionDto>> Create(SaveConventionRequest request, CancellationToken cancellationToken)
    {
        var convention = new Convention { Id = Guid.NewGuid() };
        var error = Apply(convention, request);
        if (error is not null)
            return Bad(error);
        db.Conventions.Add(convention);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/conventions/{convention.Id}", Map(convention));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<ConventionDto>> Update(Guid id, SaveConventionRequest request, CancellationToken cancellationToken)
    {
        var convention = await db.Conventions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (convention is null)
            return NotFound();
        var error = Apply(convention, request);
        if (error is not null)
            return Bad(error);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(convention));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var convention = await db.Conventions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (convention is null)
            return NotFound();
        db.Conventions.Remove(convention);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Apply(Convention convention, SaveConventionRequest request)
    {
        if (request.StartsOn is DateOnly start && request.EndsOn is DateOnly end && end < start)
            return "The end date cannot be before the start date.";
        convention.Name = request.Name.Trim();
        convention.Description = Clean(request.Description);
        convention.StartsOn = request.StartsOn;
        convention.EndsOn = request.EndsOn;
        convention.LocationText = Clean(request.LocationText);
        convention.WebsiteUrl = Clean(request.WebsiteUrl);
        convention.SortOrder = request.SortOrder;
        return null;
    }

    private static ConventionDto Map(Convention convention) => new()
    {
        Id = convention.Id,
        Name = convention.Name,
        Description = convention.Description,
        StartsOn = convention.StartsOn,
        EndsOn = convention.EndsOn,
        LocationText = convention.LocationText,
        WebsiteUrl = convention.WebsiteUrl,
        SortOrder = convention.SortOrder
    };
}
