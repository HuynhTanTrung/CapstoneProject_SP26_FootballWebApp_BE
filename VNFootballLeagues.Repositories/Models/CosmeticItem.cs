namespace VNFootballLeagues.Repositories.Models;

public class CosmeticItem
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>frame | nameColor | banner | badge | effect | card</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>shop | achievement</summary>
    public string UnlockType { get; set; } = "shop";
    public int? PointCost { get; set; }
    /// <summary>e.g. streak_7, streak_30, correct_10</summary>
    public string? AchievementKey { get; set; }
    /// <summary>CSS/gradient/image data for FE rendering</summary>
    public string? PreviewData { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<UserCosmetic> UserCosmetics { get; set; } = new List<UserCosmetic>();
}

public class UserCosmetic
{
    public int UserCosmeticId { get; set; }
    public Guid UserId { get; set; }
    public int ItemId { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
    public bool IsEquipped { get; set; } = false;

    public virtual User User { get; set; } = null!;
    public virtual CosmeticItem Item { get; set; } = null!;
}

public class UserLoadout
{
    public Guid UserId { get; set; }
    public int? FrameItemId { get; set; }
    public int? NameColorItemId { get; set; }
    public int? BannerItemId { get; set; }
    public int? BadgeItemId { get; set; }
    public int? EffectItemId { get; set; }
    public int? CardItemId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual CosmeticItem? Frame { get; set; }
    public virtual CosmeticItem? NameColor { get; set; }
    public virtual CosmeticItem? Banner { get; set; }
    public virtual CosmeticItem? Badge { get; set; }
    public virtual CosmeticItem? Effect { get; set; }
    public virtual CosmeticItem? Card { get; set; }
}
