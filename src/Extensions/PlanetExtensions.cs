using System.Collections.Generic;
using System.Linq;
using SFS.Stats;
using SFS.World;
using SFS.WorldBase;

namespace BetterPlanets.Extensions;

public static class PlanetExtensions
{
    extension(Planet planet)
    {
        public List<Rocket> GetOrbitingRockets()
        {
            return GameManager.main.rockets.Where(rocket => 
            {
                if (rocket.location.planet.Value != planet) return false;
                
                return rocket.stats.tracker.state_Orbit == StatsRecorder.Tracker.State_Orbit.Low ||
                       rocket.stats.tracker.state_Orbit == StatsRecorder.Tracker.State_Orbit.High ||
                       rocket.stats.tracker.state_Orbit == StatsRecorder.Tracker.State_Orbit.Trans;
            }).ToList();
        }
    }
}
