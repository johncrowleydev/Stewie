namespace Stewie.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    NHibernate.ISession Session { get; }
    void BeginTransaction();
    Task CommitAsync();
    void Rollback();
}
