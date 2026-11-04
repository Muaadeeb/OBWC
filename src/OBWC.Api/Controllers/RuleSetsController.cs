namespace OBWC.Api.Controllers;

[ApiController]
[Route("api/rulesets")]
public class RuleSetsController(ApplicationDbContext db, IAuthorizationService authorization) : ClubApiController
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RuleSetDto>>> List(CancellationToken cancellationToken)
    {
        var ruleSets = await db.RuleSets
            .Include(ruleSet => ruleSet.Players)
            .ThenInclude(player => player.Owner)
            .AsNoTracking()
            .OrderBy(ruleSet => ruleSet.SortOrder)
            .ThenBy(ruleSet => ruleSet.Name)
            .ToListAsync(cancellationToken);
        return Ok(ruleSets.Select(Map).ToArray());
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RuleSetDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var ruleSet = await LoadAsync(id, cancellationToken);
        return ruleSet is null ? NotFound() : Ok(Map(ruleSet));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<RuleSetDto>> Create(SaveRuleSetRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await db.RuleSets.AnyAsync(ruleSet => ruleSet.Name == name, cancellationToken))
            return ConflictProblem("A rule set with that name is already listed.");

        var entity = new RuleSet
        {
            Id = Guid.NewGuid(),
            Name = name,
            PublishedPlayers = Clean(request.PublishedPlayers),
            SortOrder = request.SortOrder
        };
        db.RuleSets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<ActionResult<RuleSetDto>> Update(Guid id, SaveRuleSetRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.RuleSets.SingleOrDefaultAsync(ruleSet => ruleSet.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();
        var name = request.Name.Trim();
        if (await db.RuleSets.AnyAsync(ruleSet => ruleSet.Id != id && ruleSet.Name == name, cancellationToken))
            return ConflictProblem("A rule set with that name is already listed.");

        entity.Name = name;
        entity.PublishedPlayers = Clean(request.PublishedPlayers);
        entity.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(await LoadAsync(id, cancellationToken) ?? entity));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.RuleSets.SingleOrDefaultAsync(ruleSet => ruleSet.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();
        db.RuleSets.Remove(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("This rule set is still used by a scheduled game.");
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/players")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<RuleSetDto>> AddSelf(Guid id, SavePlayerRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId is null)
            return Unauthorized();
        var ruleSet = await db.RuleSets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (ruleSet is null)
            return NotFound();
        if (await db.RuleSetPlayers.AnyAsync(player => player.RuleSetId == id && player.OwnerUserId == CurrentUserId, cancellationToken))
            return ConflictProblem("You are already listed for this rule set.");

        db.RuleSetPlayers.Add(new RuleSetPlayer
        {
            Id = Guid.NewGuid(),
            RuleSetId = id,
            OwnerUserId = CurrentUserId,
            Note = Clean(request.Note),
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(await LoadAsync(id, cancellationToken) ?? ruleSet));
    }

    [HttpPut("{id:guid}/players/{playerId:guid}")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<ActionResult<RuleSetDto>> UpdatePlayer(Guid id, Guid playerId, SavePlayerRequest request, CancellationToken cancellationToken)
    {
        var player = await db.RuleSetPlayers.SingleOrDefaultAsync(item => item.Id == playerId && item.RuleSetId == id, cancellationToken);
        if (player is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, player);
        if (deny is not null)
            return deny;
        player.Note = Clean(request.Note);
        await db.SaveChangesAsync(cancellationToken);
        var ruleSet = await LoadAsync(id, cancellationToken);
        return ruleSet is null ? NotFound() : Ok(Map(ruleSet));
    }

    [HttpDelete("{id:guid}/players/{playerId:guid}")]
    [Authorize(Policy = PolicyNames.Member)]
    public async Task<IActionResult> DeletePlayer(Guid id, Guid playerId, CancellationToken cancellationToken)
    {
        var player = await db.RuleSetPlayers.SingleOrDefaultAsync(item => item.Id == playerId && item.RuleSetId == id, cancellationToken);
        if (player is null)
            return NotFound();
        var deny = await DenyUnlessOwnerOrAdminAsync(authorization, player);
        if (deny is not null)
            return deny;
        db.RuleSetPlayers.Remove(player);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<RuleSet?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.RuleSets
            .Include(ruleSet => ruleSet.Players)
            .ThenInclude(player => player.Owner)
            .AsNoTracking()
            .SingleOrDefaultAsync(ruleSet => ruleSet.Id == id, cancellationToken);

    private RuleSetDto Map(RuleSet ruleSet) => new()
    {
        Id = ruleSet.Id,
        Name = ruleSet.Name,
        PublishedPlayers = ruleSet.PublishedPlayers,
        SortOrder = ruleSet.SortOrder,
        Players = ruleSet.Players
            .OrderBy(player => player.Owner?.DisplayName)
            .Select(player => new RuleSetPlayerDto
            {
                Id = player.Id,
                OwnerUserId = player.OwnerUserId,
                DisplayName = string.IsNullOrWhiteSpace(player.Owner?.DisplayName) ? "Member" : player.Owner!.DisplayName,
                Note = player.Note,
                CanEdit = IsElevated || CurrentUserId == player.OwnerUserId
            })
            .ToArray()
    };
}
