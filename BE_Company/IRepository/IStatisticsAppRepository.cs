using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IStatisticsAppRepository
    {
        Task<StatisticsAppGetDTO> StatisticsApp_GetAll();
    }
}
