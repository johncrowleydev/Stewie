using NHibernate;
using Stewie.Application.Interfaces;

namespace Stewie.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private ITransaction? _transaction;

    public UnitOfWork(ISessionFactory sessionFactory)
    {
        Session = sessionFactory.OpenSession();
    }

    public ISession Session { get; }

    public void BeginTransaction()
    {
        _transaction = Session.BeginTransaction();
    }

    public async Task CommitAsync()
    {
        if (_transaction is { IsActive: true })
        {
            await _transaction.CommitAsync();
        }
    }

    public void Rollback()
    {
        if (_transaction is { IsActive: true })
        {
            _transaction.Rollback();
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        Session.Dispose();
    }
}
