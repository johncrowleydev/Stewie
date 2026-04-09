/// <summary>
/// NHibernate mapping for the Event entity.
/// Maps to the "Events" table in SQL Server.
/// REF: BLU-001 §6
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="Event"/>.
/// </summary>
public class EventMap : ClassMap<Event>
{
    /// <summary>
    /// Initializes the Event-to-Events table mapping.
    /// EventType is stored as an integer via CustomType for enum persistence.
    /// </summary>
    public EventMap()
    {
        Table("Events");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.EntityType).Not.Nullable();
        Map(x => x.EntityId).Not.Nullable();
        Map(x => x.EventType).CustomType<EventType>().Not.Nullable();
        Map(x => x.Payload).Length(4000);
        Map(x => x.Timestamp).Not.Nullable();
    }
}
