using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;

namespace Stewie.Infrastructure.Persistence;

public static class NHibernateHelper
{
    public static ISessionFactory BuildSessionFactory(string connectionString)
    {
        return Fluently.Configure()
            .Database(MsSqlConfiguration.MsSql2012
                .ConnectionString(connectionString)
                .Driver<NHibernate.Driver.MicrosoftDataSqlClientDriver>()
                .ShowSql())
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<Mappings.JobMap>())
            .BuildSessionFactory();
    }
}
