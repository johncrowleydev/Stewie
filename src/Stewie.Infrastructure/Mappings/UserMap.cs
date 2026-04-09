using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>FluentNHibernate mapping for <see cref="User"/>.</summary>
public class UserMap : ClassMap<User>
{
    public UserMap()
    {
        Table("Users");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Username).Not.Nullable().Unique();
        Map(x => x.PasswordHash).Not.Nullable();
        Map(x => x.Role).CustomType<UserRole>();
        Map(x => x.CreatedAt);
    }
}
