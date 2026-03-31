using System.Linq;
using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IChargingStationRepository
{
    IQueryable<ChargingStation> GetStationsWithConnectors();
}
