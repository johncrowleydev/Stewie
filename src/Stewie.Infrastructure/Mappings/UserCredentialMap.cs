using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>FluentNHibernate mapping for <see cref="UserCredential"/>.</summary>
public class UserCredentialMap : ClassMap<UserCredential>
{
    public UserCredentialMap()
    {
        Table("UserCredentials");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.UserId);
        Map(x => x.Provider).Not.Nullable();
        Map(x => x.EncryptedToken).Length(4000).Not.Nullable();
        Map(x => x.CreatedAt);
        Map(x => x.UpdatedAt);
    }
}
