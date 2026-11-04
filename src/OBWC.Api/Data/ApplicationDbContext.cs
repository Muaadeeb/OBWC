namespace OBWC.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();
    public DbSet<SessionAttendance> SessionAttendances => Set<SessionAttendance>();
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();
    public DbSet<RuleSetPlayer> RuleSetPlayers => Set<RuleSetPlayer>();
    public DbSet<GameStore> GameStores => Set<GameStore>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<GamingGroup> GamingGroups => Set<GamingGroup>();
    public DbSet<Convention> Conventions => Set<Convention>();
    public DbSet<GalleryPhoto> GalleryPhotos => Set<GalleryPhoto>();
    public DbSet<EventPhoto> EventPhotos => Set<EventPhoto>();
    public DbSet<ClubDocument> ClubDocuments => Set<ClubDocument>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Officer> Officers => Set<Officer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.MembershipType).HasConversion<string>().HasMaxLength(32);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(token => token.UserId).HasMaxLength(450).IsRequired();
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.ReplacedByHash).HasMaxLength(128);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlaySession>(entity =>
        {
            entity.ToTable("PlaySessions");
            entity.Property(session => session.Title).HasMaxLength(160);
            entity.Property(session => session.Notes).HasMaxLength(1000);
            entity.Property(session => session.Venue).HasMaxLength(160);
            entity.Property(session => session.Address).HasMaxLength(300);
            entity.Property(session => session.OnlineLink).HasMaxLength(500);
            entity.Property(session => session.LocationNotes).HasMaxLength(1000);
            entity.Property(session => session.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(session => session.CancelledByUserId).HasMaxLength(450);
            entity.Property(session => session.CancellationReason).HasMaxLength(500);
            entity.Property(session => session.IsPublished).HasDefaultValue(true);
            entity.Property(session => session.AllowPlayerRsvp).HasDefaultValue(true);
            entity.Property(session => session.IsCancelled).HasDefaultValue(false);
            entity.HasIndex(session => session.ScheduledAt);
            entity.HasIndex(session => session.SeriesKey);
            entity.HasOne(session => session.CreatedBy)
                .WithMany()
                .HasForeignKey(session => session.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.RuleSet)
                .WithMany()
                .HasForeignKey(session => session.RuleSetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(session => session.Attendances)
                .WithOne(row => row.PlaySession!)
                .HasForeignKey(row => row.PlaySessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(session => session.Photos)
                .WithOne(photo => photo.PlaySession!)
                .HasForeignKey(photo => photo.PlaySessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessionAttendance>(entity =>
        {
            entity.ToTable("SessionAttendances");
            entity.Property(row => row.UserId).HasMaxLength(450).IsRequired();
            entity.Property(row => row.CheckedInByUserId).HasMaxLength(450);
            entity.Property(row => row.MarkedByUserId).HasMaxLength(450);
            entity.Property(row => row.Notes).HasMaxLength(500);
            entity.Property(row => row.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(row => row.RsvpStatus).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(row => new { row.PlaySessionId, row.UserId }).IsUnique();
            entity.HasOne(row => row.User)
                .WithMany()
                .HasForeignKey(row => row.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RuleSet>(entity =>
        {
            entity.Property(ruleSet => ruleSet.Name).HasMaxLength(200).IsRequired();
            entity.Property(ruleSet => ruleSet.PublishedPlayers).HasMaxLength(2000);
            entity.HasIndex(ruleSet => ruleSet.Name).IsUnique();
            entity.HasMany(ruleSet => ruleSet.Players)
                .WithOne(player => player.RuleSet!)
                .HasForeignKey(player => player.RuleSetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RuleSetPlayer>(entity =>
        {
            entity.Property(player => player.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.Property(player => player.Note).HasMaxLength(500);
            entity.HasIndex(player => new { player.RuleSetId, player.OwnerUserId }).IsUnique();
            entity.HasOne(player => player.Owner)
                .WithMany()
                .HasForeignKey(player => player.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GameStore>(entity =>
        {
            entity.Property(store => store.Name).HasMaxLength(200).IsRequired();
            entity.Property(store => store.FormerName).HasMaxLength(200);
            entity.Property(store => store.Address).HasMaxLength(300);
            entity.Property(store => store.Description).HasMaxLength(2000);
            entity.Property(store => store.Bullet1).HasMaxLength(300);
            entity.Property(store => store.Bullet2).HasMaxLength(300);
            entity.Property(store => store.Bullet3).HasMaxLength(300);
        });

        builder.Entity<Venue>(entity =>
        {
            entity.Property(venue => venue.Name).HasMaxLength(200).IsRequired();
            entity.Property(venue => venue.Address).HasMaxLength(300);
            entity.Property(venue => venue.Description).HasMaxLength(2000);
            entity.Property(venue => venue.InsidePhotoPath).HasMaxLength(400);
            entity.Property(venue => venue.InsideContentType).HasMaxLength(100);
            entity.Property(venue => venue.OutsidePhotoPath).HasMaxLength(400);
            entity.Property(venue => venue.OutsideContentType).HasMaxLength(100);
        });

        builder.Entity<GamingGroup>(entity =>
        {
            entity.Property(group => group.Name).HasMaxLength(200).IsRequired();
            entity.Property(group => group.Description).HasMaxLength(4000);
            entity.Property(group => group.GameFocus).HasMaxLength(200);
            entity.Property(group => group.WebsiteUrl).HasMaxLength(500);
            entity.Property(group => group.Contact).HasMaxLength(200);
        });

        builder.Entity<Convention>(entity =>
        {
            entity.Property(convention => convention.Name).HasMaxLength(200).IsRequired();
            entity.Property(convention => convention.Description).HasMaxLength(4000);
            entity.Property(convention => convention.LocationText).HasMaxLength(300);
            entity.Property(convention => convention.WebsiteUrl).HasMaxLength(500);
        });

        builder.Entity<EventPhoto>(entity =>
        {
            entity.Property(photo => photo.Caption).HasMaxLength(200).IsRequired();
            entity.Property(photo => photo.RelativePath).HasMaxLength(400).IsRequired();
            entity.Property(photo => photo.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(photo => photo.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(photo => photo.PlaySessionId);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(photo => photo.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GalleryPhoto>(entity =>
        {
            entity.Property(photo => photo.Caption).HasMaxLength(200).IsRequired();
            entity.Property(photo => photo.PublicEventName).HasMaxLength(200).IsRequired();
            entity.Property(photo => photo.RelativePath).HasMaxLength(400).IsRequired();
            entity.Property(photo => photo.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(photo => photo.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(photo => photo.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClubDocument>(entity =>
        {
            entity.Property(document => document.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(document => document.Title).HasMaxLength(200).IsRequired();
            entity.Property(document => document.Description).HasMaxLength(2000);
            entity.Property(document => document.FileName).HasMaxLength(260).IsRequired();
            entity.Property(document => document.RelativePath).HasMaxLength(400).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(document => document.Visibility).HasConversion<string>().HasMaxLength(32);
            entity.Property(document => document.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(document => document.Category);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(document => document.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContentPage>(entity =>
        {
            entity.HasKey(page => page.Key);
            entity.Property(page => page.Key).HasMaxLength(50);
            entity.Property(page => page.Title).HasMaxLength(200).IsRequired();
            entity.Property(page => page.Body).HasMaxLength(20000).IsRequired();
        });

        builder.Entity<SiteSetting>(entity =>
        {
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).HasMaxLength(100);
            entity.Property(setting => setting.Value).HasMaxLength(500).IsRequired();
        });

        builder.Entity<Officer>(entity =>
        {
            entity.Property(officer => officer.RoleTitle).HasMaxLength(100).IsRequired();
            entity.Property(officer => officer.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(officer => officer.TermLabel).HasMaxLength(100);
            entity.Property(officer => officer.Contact).HasMaxLength(200);
            entity.Property(officer => officer.PhotoPath).HasMaxLength(400);
            entity.Property(officer => officer.PhotoContentType).HasMaxLength(100);
        });
    }
}
