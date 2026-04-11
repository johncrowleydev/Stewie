/// <summary>
/// NHibernate mapping for the ChatMessage entity.
/// Maps to the "ChatMessages" table in SQL Server.
/// REF: JOB-013 T-131
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="ChatMessage"/>.
/// </summary>
public class ChatMessageMap : ClassMap<ChatMessage>
{
    /// <summary>
    /// Initializes the ChatMessage-to-ChatMessages table mapping.
    /// </summary>
    public ChatMessageMap()
    {
        Table("ChatMessages");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.ProjectId);
        Map(x => x.SenderRole).Length(50);
        Map(x => x.SenderName).Length(100);
        Map(x => x.Content).Length(10001);
        Map(x => x.CreatedAt);
        Map(x => x.MessageType).Length(50).Nullable();
    }
}
