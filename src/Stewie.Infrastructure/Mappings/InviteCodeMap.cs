using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>FluentNHibernate mapping for <see cref="InviteCode"/>.</summary>
public class InviteCodeMap : ClassMap<InviteCode>
{
    public InviteCodeMap()
    {
        Table("InviteCodes");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Code).Not.Nullable().Unique();
        Map(x => x.CreatedByUserId);
        Map(x => x.UsedByUserId);
        Map(x => x.UsedAt);
        Map(x => x.ExpiresAt);
        Map(x => x.CreatedAt);
    }
}
