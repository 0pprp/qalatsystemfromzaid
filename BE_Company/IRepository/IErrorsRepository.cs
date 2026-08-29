using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IErrorsRepository
    {
        Task<Guid> Create(Error error);
    }
}
