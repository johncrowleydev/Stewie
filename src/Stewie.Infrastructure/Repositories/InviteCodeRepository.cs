using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>NHibernate implementation of <see cref="IInviteCodeRepository"/>.</summary>
public class InviteCodeRepository : IInviteCodeRepository
{
    private readonly IUnitOfWork _uow;
    public InviteCodeRepository(IUnitOfWork uow) => _uow = uow;
    public async Task SaveAsync(InviteCode inviteCode) => await _uow.Session.SaveOrUpdateAsync(inviteCode);
    public async Task<InviteCode?> GetByCodeAsync(string code) =>
        await _uow.Session.Query<InviteCode>().FirstOrDefaultAsync(i => i.Code == code);
    public async Task<IList<InviteCode>> GetAllAsync() =>
        await _uow.Session.Query<InviteCode>().OrderByDescending(i => i.CreatedAt).ToListAsync();
}
