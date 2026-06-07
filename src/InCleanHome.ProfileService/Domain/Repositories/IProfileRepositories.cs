using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Domain.Model.Queries;

namespace InCleanHome.ProfileService.Domain.Repositories;

public interface IClientProfileRepository : IBaseRepository<ClientProfile>
{
    Task<ClientProfile?> FindByUserIdAsync(int userId);
}

public interface IWorkerProfileRepository : IBaseRepository<WorkerProfile>
{
    Task<WorkerProfile?> FindByUserIdAsync(int userId);
    Task<IEnumerable<WorkerProfile>> SearchAsync(SearchWorkersQuery filters);
}
