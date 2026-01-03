using System;
using System.Collections.Generic;
using FixMath;
using MiniMotorwaysModLoader;
using Motorways;
using Motorways.Audio;
using Motorways.Models;
using UnityEngine;

/// <summary>
/// RunINFO: Displays upcoming building spawn timers on screen.
/// Press F11 to toggle visibility.
/// </summary>
public class RunINFO : IMod, IConfigurableMod
{
    public string Name => "RunINFO";
    public string Version => "0.0.1";
    public string Author => "Roofus";

    private static GameObject _uiObject;
    private static RunINFOUI _ui;
    
    // Config entries
    private List<ModConfigEntry> _configEntries = new List<ModConfigEntry>();
    
    public void OnLoad()
    {
        _uiObject = new GameObject("RunINFO");
        _ui = _uiObject.AddComponent<RunINFOUI>();
        UnityEngine.Object.DontDestroyOnLoad(_uiObject);
        
        // Initialize config entries
        InitializeConfigEntries();
    }

    public void OnUnload()
    {
        if (_uiObject != null)
        {
            UnityEngine.Object.Destroy(_uiObject);
        }
    }
    
    private void InitializeConfigEntries()
    {
        _configEntries = new List<ModConfigEntry>
        {
            ModConfigEntry.CreateBool("show_efficiency", "Show Efficiency", true, "Display efficiency percentage and breakdown"),
            ModConfigEntry.CreateBool("show_game_efficiency", "Show Game Efficiency", true, "Display game's built-in efficiency score"),
            ModConfigEntry.CreateBool("show_overcrowding", "Show Overcrowding", true, "Display overcrowding timer info"),
            ModConfigEntry.CreateBool("show_traffic", "Show Traffic", true, "Display traffic flow information"),
            ModConfigEntry.CreateBool("show_supply", "Show Supply", true, "Display supply percentage"),
            ModConfigEntry.CreateBool("show_spawn_ramp", "Show Spawn Ramp", true, "Display spawn ramp multiplier"),
            ModConfigEntry.CreateBool("show_timers", "Show Timers", true, "Display building spawn timers"),
            ModConfigEntry.CreateBool("show_expandable", "Show Expandable", false, "Show expandable efficiency details (click to expand)")
        };
    }
    
    public List<ModConfigEntry> GetConfigEntries()
    {
        return _configEntries;
    }
    
    public void OnConfigChanged(string key, object value)
    {
        if (_ui != null)
        {
            _ui.ApplyConfig(key, value);
        }
    }
    
    public void LoadConfig(Dictionary<string, string> savedValues)
    {
        foreach (var entry in _configEntries)
        {
            if (savedValues.TryGetValue(entry.Key, out var value))
            {
                entry.Deserialize(value);
            }
        }
        
        // apply loaded config to UI
        if (_ui != null)
        {
            foreach (var entry in _configEntries)
            {
                _ui.ApplyConfig(entry.Key, entry.CurrentValue);
            }
        }
    }
}


/// <summary>
/// UI component that renders spawn timers using IMGUI.
/// </summary>
public class RunINFOUI : MonoBehaviour
{
    private bool _visible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;

    private List<string> _displayLines = new List<string>();
    private int _supplyLineIndex = -1; // Track which line is the supply line
    private float _supplyPercent = 100f; // Track supply percentage for coloring
    
    private int _trafficLineIndex = -1;
    private float _trafficFlowPercent = 100f;
    private int _stuckCarCount = 0;
    
    // Advanced stuck car tracking (vehicle ID -> time first detected stuck)
    private Dictionary<int, float> _stuckVehicleTimers = new Dictionary<int, float>();
    private float _lastStuckCheck = 0f;
    private const float STUCK_CHECK_INTERVAL = 0.5f; // Check every 0.5s, not every frame
    private const float STUCK_THRESHOLD = 3.0f; // Must be stuck for 3s to count
    private int _confirmedStuckCars = 0;
    
    // Overcrowding tracking
    private int _overcrowdLineIndex = -1;
    private float _overcrowdPercent = 0f; // 0-100, where 100 = about to fail
    
    // Spawn ramp tracking  
    private int _spawnRampLineIndex = -1;
    private float _spawnRampValue = 1f;
    
    // Comprehensive Efficiency tracking
    private int _efficiencyLineIndex = -1;
    private float _overallEfficiency = 100f;
    private float _lastEfficiencyCheck = 0f;
    private const float EFFICIENCY_CHECK_INTERVAL = 1.0f;
    private const int HISTORY_SIZE = 10;
    
    // 1. Timer Trend & Stability (existing)
    private List<float> _timerDeltaHistory = new List<float>();
    private float _previousTimerSum = 0f;
    private float _trendScore = 100f;
    private float _stabilityScore = 100f;
    
    // 2. Pin Delivery (existing)
    private int _previousTotalPins = 0;
    private List<float> _pinChangeHistory = new List<float>();
    private float _deliveryScore = 100f;
    
    // 3. Trip Efficiency - average path length (shorter = better)
    private List<float> _tripLengthHistory = new List<float>();
    private float _avgTripLength = 0f;
    private float _tripScore = 100f;
    
    // 4. Total Serviced Pins - productivity tracking
    private int _previousServicedPins = 0;
    private int _pinsServicedPerSecond = 0;
    private float _serviceRateScore = 100f;
    
    // 5. Intersection Wait Time - traffic light efficiency
    private float _avgWaitTime = 0f;
    private float _waitTimeScore = 100f;
    
    // 6. Lane Utilization - traffic distribution
    private float _maxLaneLoad = 0f;
    private float _avgLaneLoad = 0f;
    private float _utilizationScore = 100f;
    
    // 7. Carpark Queue - destination congestion
    private int _totalCarparkQueue = 0;
    private float _carparkScore = 100f;
    
    // 8. Repathing Frequency - network stability
    private int _repathingCars = 0;
    private float _networkStabilityScore = 100f;
    
    // 9. Game's own efficiency score (from ScoreModel)
    private float _gameEfficiencyScore = 0f;
    private int _gameEfficiencyLineIndex = -1;
    
    // Config visibility flags (controlled by ModManager)
    private bool _showEfficiency = true;
    private bool _showGameEfficiency = true;
    private bool _showOvercrowding = true;
    private bool _showTraffic = true;
    private bool _showSupply = true;
    private bool _showSpawnRamp = true;
    private bool _showTimers = true;
    private bool _showExpandable = false;
    
    /// <summary>
    /// Apply a configuration change from ModManager
    /// </summary>
    public void ApplyConfig(string key, object value)
    {
        bool boolVal = value is bool b ? b : false;
        
        switch (key)
        {
            case "show_efficiency":
                _showEfficiency = boolVal;
                break;
            case "show_game_efficiency":
                _showGameEfficiency = boolVal;
                break;
            case "show_overcrowding":
                _showOvercrowding = boolVal;
                break;
            case "show_traffic":
                _showTraffic = boolVal;
                break;
            case "show_supply":
                _showSupply = boolVal;
                break;
            case "show_spawn_ramp":
                _showSpawnRamp = boolVal;
                break;
            case "show_timers":
                _showTimers = boolVal;
                break;
            case "show_expandable":
                _showExpandable = boolVal;
                break;
        }
    }

    private void Update()
    {
        // toggle visibility with F11
        if (Input.GetKeyDown(KeyCode.F11))
        {
            _visible = !_visible;
        }

        UpdateSpawnInfo();
    }

    private void UpdateSpawnInfo()
    {
        _displayLines.Clear();
        
        try
        {
            var city = Get.City;
            if (city == null)
            {
                _displayLines.Add("No city");
                return;
            }

            if (city.Scope == null)
            {
                _displayLines.Add("No scope");
                return;
            }

            var cityPlan = city.Scope.Get<CityPlanModel>();
            var clock = Get.Clock;
            var simulation = city.Scope.Get<Server.ISimulation>();

            if (cityPlan == null || clock == null || simulation == null)
            {
                _displayLines.Add("Loading...");
                return;
            }

            Fix64 currentTime = clock.ExpansionTime;
            Fix64 nextHouseTime = Fix64.MaxValue;
            Fix64 nextDestTime = Fix64.MaxValue;
            int houseGroupIndex = -1;
            int destGroupIndex = -1;
            int houseCount = 0;
            int destCount = 0;

            // Scan scheduled buildings
            if (cityPlan.scheduledBuildings != null)
            {
                int totalScanned = 0;
                foreach (var scheduled in cityPlan.scheduledBuildings)
                {
                    if (scheduled == null) continue;
                    totalScanned++;
                    
                    if (scheduled.time > currentTime)
                    {
                        if (scheduled.type == CityTileType.Supply)
                        {
                            houseCount++;
                            if (scheduled.time < nextHouseTime)
                            {
                                nextHouseTime = scheduled.time;
                                houseGroupIndex = scheduled.groupIndex;
                            }
                        }
                        else if (scheduled.type == CityTileType.Demand)
                        {
                            destCount++;
                            if (scheduled.time < nextDestTime)
                            {
                                nextDestTime = scheduled.time;
                                destGroupIndex = scheduled.groupIndex;
                            }
                        }
                    }
                }
            }

            // House status
            if (_showTimers)
            {
                if (nextHouseTime < Fix64.MaxValue)
                {
                    float secondsUntilHouse = (float)(nextHouseTime - currentTime);
                    _displayLines.Add($"House: {secondsUntilHouse:F1}s (G{houseGroupIndex})");
                }
                else
                {
                    int destsNeedingSupply = 0;
                    Server.ModelListEnumerator<DestinationModel> destEnum = simulation.GetModels<DestinationModel>().GetEnumerator();
                    while (destEnum.MoveNext())
                    {
                        var dest = destEnum.Current;
                        if (dest != null && dest.isActive && !dest.IsSupplySufficient)
                        {
                            destsNeedingSupply++;
                        }
                    }
                    
                    if (destsNeedingSupply > 0)
                    {
                        _displayLines.Add($"House: Waiting ({destsNeedingSupply} low supply)");
                    }
                    else if (houseCount > 0)
                    {
                        _displayLines.Add($"House: {houseCount} queued");
                    }
                    else
                    {
                        _displayLines.Add("House: All supplied");
                    }
                }
            }

            // Destination status
            if (_showTimers)
            {
                if (nextDestTime < Fix64.MaxValue)
                {
                    float secondsUntilDest = (float)(nextDestTime - currentTime);
                    _displayLines.Add($"Dest: {secondsUntilDest:F1}s (G{destGroupIndex})");
                }
                else if (destCount > 0)
                {
                    _displayLines.Add($"Dest: {destCount} queued");
                }
                else
                {
                    _displayLines.Add("Dest: None scheduled");
                }

                // Check for upcoming upgrades
                Fix64 nextUpgradeTime = Fix64.MaxValue;
                int upgradeGroupIndex = -1;
                Server.ModelListEnumerator<DestinationModel> upgradeEnum = simulation.GetModels<DestinationModel>().GetEnumerator();
                while (upgradeEnum.MoveNext())
                {
                    var dest = upgradeEnum.Current;
                    if (dest != null && dest.isActive && dest.IsScheduledToBeUpgraded)
                    {
                        if (dest.demandLevelUpTime < nextUpgradeTime)
                        {
                            nextUpgradeTime = dest.demandLevelUpTime;
                            upgradeGroupIndex = dest.GroupIndex;
                        }
                    }
                }

                if (nextUpgradeTime < Fix64.MaxValue)
                {
                    float secondsUntilUpgrade = (float)(nextUpgradeTime - currentTime);
                    _displayLines.Add($"Upgrade: {secondsUntilUpgrade:F1}s (G{upgradeGroupIndex})");
                }
            }

            // Calculate overall supply status
            Fix64 totalSupply = Fix64.Zero;
            Fix64 totalRequired = Fix64.Zero;
            int totalPinsWaiting = 0;
            
            Server.ModelListEnumerator<DestinationModel> supplyEnum = simulation.GetModels<DestinationModel>().GetEnumerator();
            while (supplyEnum.MoveNext())
            {
                var dest = supplyEnum.Current;
                if (dest != null && dest.isActive)
                {
                    totalSupply += dest.contributedSupply;
                    totalRequired += dest.RequiredSupply;
                    totalPinsWaiting += dest.TotalDemand;
                }
            }

            if (_showSupply && totalRequired > Fix64.Zero)
            {
                float supplyPercent = (float)(totalSupply / totalRequired * (Fix64)100);
                _supplyPercent = supplyPercent; // Store for coloring
                float deficit = (float)(totalRequired - totalSupply);
                
                _supplyLineIndex = _displayLines.Count; // Mark which line is supply
                if (supplyPercent < 100f)
                {
                    _displayLines.Add($"Supply: {supplyPercent:F0}% (needs {deficit:F0} more)");
                }
                else
                {
                    _displayLines.Add($"Supply: {supplyPercent:F0}% ({totalPinsWaiting} pins)");
                }
            }
            else
            {
                _supplyLineIndex = -1; // no supply line
            }

            // --- Spawn Ramp (Difficulty Multiplier) ---
            if (_showSpawnRamp)
            {
                var demandModel = city.Scope.Get<DemandModel>();
                if (demandModel != null)
                {
                    _spawnRampValue = (float)demandModel.spawnScale;
                    _spawnRampLineIndex = _displayLines.Count;
                    _displayLines.Add($"Spawn Ramp: {_spawnRampValue:F2}x");
                }
            }

            // --- Overcrowding Timer
            // find the destination with the lowest remaining time (highest priority)
            float worstOvercrowdTime = 0f;
            float maxOvercrowdTime = 90f; // from SimulationConstantsData.MaxOvercrowdTime
            int worstOvercrowdGroup = -1;
            bool anyOvercrowding = false;
            
            Server.ModelListEnumerator<DestinationModel> overcrowdEnum = simulation.GetModels<DestinationModel>().GetEnumerator();
            while (overcrowdEnum.MoveNext())
            {
                var dest = overcrowdEnum.Current;
                if (dest != null && dest.isActive && dest.IsOvercrowding)
                {
                    anyOvercrowding = true;
                    float currentOvercrowd = (float)dest.CurrentFrame.OvercrowdingTime;
                    if (currentOvercrowd > worstOvercrowdTime)
                    {
                        worstOvercrowdTime = currentOvercrowd;
                        worstOvercrowdGroup = dest.GroupIndex;
                    }
                }
            }
            
            if (_showOvercrowding && anyOvercrowding)
            {
                _overcrowdPercent = (worstOvercrowdTime / maxOvercrowdTime) * 100f;
                _overcrowdLineIndex = _displayLines.Count;
                float timeRemaining = maxOvercrowdTime - worstOvercrowdTime;
                
                if (timeRemaining < 10f)
                {
                    _displayLines.Add($"CRITICAL G{worstOvercrowdGroup}: {timeRemaining:F1}s!");
                }
                else
                {
                    _displayLines.Add($"Overcrowd G{worstOvercrowdGroup}: {timeRemaining:F0}s left");
                }
            }
            else
            {
                _overcrowdLineIndex = -1;
                _overcrowdPercent = 0f;
            }

            // only check every seconds
            bool doStuckCheck = (Time.time - _lastStuckCheck) >= STUCK_CHECK_INTERVAL;
            if (doStuckCheck)
            {
                _lastStuckCheck = Time.time;
                
                HashSet<int> currentlyStuck = new HashSet<int>();
                
                Server.ModelListEnumerator<VehicleModel> vehicleEnum = simulation.GetModels<VehicleModel>().GetEnumerator();
                while (vehicleEnum.MoveNext())
                {
                    var vehicle = vehicleEnum.Current;
                    if (vehicle != null && !vehicle.IsParkedAtDestination && !vehicle.IsWaitingAtHouse)
                    {
                        // Check if car is stuck (very low speed while driving)
                        if (vehicle.CurrentFrame.speed < (Fix64)0.01 && 
                            (vehicle.behaviorState == VehicleModel.BehaviorState.DrivingToDestination || 
                             vehicle.behaviorState == VehicleModel.BehaviorState.DrivingHome))
                        {
                            int carId = vehicle.id;
                            currentlyStuck.Add(carId);
                            
                            // add to tracking if new
                            if (!_stuckVehicleTimers.ContainsKey(carId))
                            {
                                _stuckVehicleTimers[carId] = Time.time;
                            }
                        }
                    }
                }
                
                // remove cars that are no longer stuck
                var toRemove = new List<int>();
                foreach (var kvp in _stuckVehicleTimers)
                {
                    if (!currentlyStuck.Contains(kvp.Key))
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var id in toRemove)
                {
                    _stuckVehicleTimers.Remove(id);
                }
                
                // count cars stuck for more than 3 seconds
                _confirmedStuckCars = 0;
                foreach (var kvp in _stuckVehicleTimers)
                {
                    if (Time.time - kvp.Value >= STUCK_THRESHOLD)
                    {
                        _confirmedStuckCars++;
                    }
                }
            }
            
            // display gridlock warning if 5+ cars are stuck
            if (_showTraffic)
            {
                if (_confirmedStuckCars >= 5)
                {
                    _trafficLineIndex = _displayLines.Count;
                    _stuckCarCount = _confirmedStuckCars;
                    _displayLines.Add($"GRIDLOCK: {_confirmedStuckCars} cars stuck!");
                }
                else if (_confirmedStuckCars > 0)
                {
                    _trafficLineIndex = _displayLines.Count;
                    _stuckCarCount = _confirmedStuckCars;
                    _displayLines.Add($"Traffic: {_confirmedStuckCars} slow");
                }
                else
                {
                    _trafficLineIndex = -1;
                    _stuckCarCount = 0;
                }
            }
            else
            {
                _trafficLineIndex = -1;
                _stuckCarCount = 0;
            }

            bool doEfficiencyCheck = (Time.time - _lastEfficiencyCheck) >= EFFICIENCY_CHECK_INTERVAL;
            if (doEfficiencyCheck)
            {
                _lastEfficiencyCheck = Time.time;
                
                // === collect data ===
                float currentTimerSum = 0f;
                int currentTotalPins = 0;
                int currentServicedPins = 0;
                int carparkQueueTotal = 0;
                int repathingCount = 0;
                float totalWaitTime = 0f;
                int waitingVehicles = 0;
                Dictionary<int, int> laneVehicleCounts = new Dictionary<int, int>();
                float totalTripLength = 0f;
                int tripCount = 0;
                
                // Collect destination data
                Server.ModelListEnumerator<DestinationModel> destEnum = simulation.GetModels<DestinationModel>().GetEnumerator();
                while (destEnum.MoveNext())
                {
                    var dest = destEnum.Current;
                    if (dest != null && dest.isActive)
                    {
                        currentTotalPins += dest.TotalDemand;
                        currentServicedPins += dest.totalServicedPins;
                        if (dest.IsOvercrowding)
                        {
                            currentTimerSum += (float)dest.CurrentFrame.OvercrowdingTime;
                        }
                        // carpark queue
                        if (dest.Carpark != null)
                        {
                            carparkQueueTotal += dest.Carpark.vehiclesEntering.Count;
                            carparkQueueTotal += dest.Carpark.vehiclesDrivingThrough.Count;
                        }
                    }
                }
                
                // Collect vehicle data
                Server.ModelListEnumerator<VehicleModel> vehicleEnum = simulation.GetModels<VehicleModel>().GetEnumerator();
                while (vehicleEnum.MoveNext())
                {
                    var vehicle = vehicleEnum.Current;
                    if (vehicle != null)
                    {
                        // repathing check
                        if (vehicle.repathUrgency != VehicleModel.PathfindUrgency.NotRequired)
                        {
                            repathingCount++;
                        }
                        
                        // Trip length (for cars that have completed trips, look at pathLengthAtStartOfJourney)
                        if (vehicle.pathLengthAtStartOfJourney > (Fix64)0)
                        {
                            totalTripLength += (float)vehicle.pathLengthAtStartOfJourney;
                            tripCount++;
                        }
                        
                        // lane utilization
                        var lane = vehicle.CurrentFrame.lane;
                        if (lane != null)
                        {
                            int laneId = lane._id;
                            if (!laneVehicleCounts.ContainsKey(laneId))
                                laneVehicleCounts[laneId] = 0;
                            laneVehicleCounts[laneId]++;
                        }
                    }
                }
                
                // collect road chunk data for wait times
                Server.ModelListEnumerator<RoadChunkModel> chunkEnum = simulation.GetModels<RoadChunkModel>().GetEnumerator();
                while (chunkEnum.MoveNext())
                {
                    var chunk = chunkEnum.Current;
                    if (chunk != null && chunk.inboundVehicles != null)
                    {
                        foreach (var inbound in chunk.inboundVehicles)
                        {
                            if (inbound.committedTimestamp > (Fix64)0)
                            {
                                float wait = (float)(clock.Time - inbound.committedTimestamp);
                                if (wait > 0)
                                {
                                    totalWaitTime += wait;
                                    waitingVehicles++;
                                }
                            }
                        }
                    }
                }
                
                // === 1. timer trend score
                float timerDelta = currentTimerSum - _previousTimerSum;
                _previousTimerSum = currentTimerSum;
                _timerDeltaHistory.Add(timerDelta);
                if (_timerDeltaHistory.Count > HISTORY_SIZE) _timerDeltaHistory.RemoveAt(0);
                
                if (_timerDeltaHistory.Count >= 3)
                {
                    float avgDelta = 0f;
                    foreach (float d in _timerDeltaHistory) avgDelta += d;
                    avgDelta /= _timerDeltaHistory.Count;
                    // +1 delta = -25 score, +4 = 0%
                    _trendScore = (avgDelta <= 0) ? 100f : Mathf.Max(0, 100f - avgDelta * 25f);
                }
                else
                {
                    _trendScore = 100f;
                }
                
                // === 2. stability score
                if (_timerDeltaHistory.Count >= 3)
                {
                    float avg = 0f;
                    foreach (float d in _timerDeltaHistory) avg += d;
                    avg /= _timerDeltaHistory.Count;
                    float variance = 0f;
                    foreach (float d in _timerDeltaHistory) variance += (d - avg) * (d - avg);
                    float stdDev = Mathf.Sqrt(variance / _timerDeltaHistory.Count);
                    _stabilityScore = Mathf.Max(0, 100f - stdDev * 15f);
                }
                else
                {
                    _stabilityScore = 100f;
                }
                
                // === 3. delivery score
                // pins accumulating = very bad
                int pinChange = currentTotalPins - _previousTotalPins;
                _previousTotalPins = currentTotalPins;
                _pinChangeHistory.Add(pinChange);
                if (_pinChangeHistory.Count > HISTORY_SIZE) _pinChangeHistory.RemoveAt(0);
                
                bool hasPinData = currentTotalPins > 0;
                if (_pinChangeHistory.Count >= 3 && hasPinData)
                {
                    float avgChange = 0f;
                    foreach (float p in _pinChangeHistory) avgChange += p;
                    avgChange /= _pinChangeHistory.Count;
                    // +1 pin/sec = -20 score, +5 = 0%
                    _deliveryScore = (avgChange <= 0) ? 100f : Mathf.Max(0, 100f - avgChange * 20f);
                }
                else
                {
                    _deliveryScore = 100f;
                }
                
                // === 4. service rate score
                _pinsServicedPerSecond = currentServicedPins - _previousServicedPins;
                _previousServicedPins = currentServicedPins;
                if (currentTotalPins == 0)
                {
                    _serviceRateScore = 100f;
                }
                else if (_pinsServicedPerSecond > 0)
                {
                    // servicing pins = good, but scale by how many exist
                    float ratio = (float)_pinsServicedPerSecond / Mathf.Max(1f, currentTotalPins);
                    _serviceRateScore = Mathf.Min(100f, ratio * 200f); // need to service 50% per sec for 100%
                }
                else
                {
                    // no pins serviced when there's demand = bad
                    // more pins waiting = worse score
                    _serviceRateScore = Mathf.Max(0, 80f - currentTotalPins * 4f);
                }
                
                // === 5. trip efficiency
                _avgTripLength = (tripCount > 0) ? totalTripLength / tripCount : 0f;
                bool hasTripData = tripCount > 0;
                if (hasTripData)
                {
                    if (_avgTripLength <= 10f)
                        _tripScore = 100f;
                    else if (_avgTripLength <= 30f)
                        _tripScore = 100f - ((_avgTripLength - 10f) / 20f * 50f);
                    else
                        _tripScore = Mathf.Max(0, 50f - ((_avgTripLength - 30f) * 2f));
                }
                else
                {
                    _tripScore = 100f;
                }
                
                // === 6. wait time score
                _avgWaitTime = (waitingVehicles > 0) ? totalWaitTime / waitingVehicles : 0f;
                bool hasWaitData = waitingVehicles > 0;
                if (hasWaitData)
                {
                    if (_avgWaitTime <= 2f)
                        _waitTimeScore = 100f;
                    else if (_avgWaitTime <= 8f)
                        _waitTimeScore = 100f - ((_avgWaitTime - 2f) / 6f * 60f);
                    else
                        _waitTimeScore = Mathf.Max(0, 40f - ((_avgWaitTime - 8f) * 5f));
                }
                else
                {
                    _waitTimeScore = 100f;
                }
                
                // === 7. lane utilization score
                bool hasLaneData = laneVehicleCounts.Count > 0;
                if (hasLaneData)
                {
                    _maxLaneLoad = 0f;
                    float totalLoad = 0f;
                    foreach (var kvp in laneVehicleCounts)
                    {
                        if (kvp.Value > _maxLaneLoad) _maxLaneLoad = kvp.Value;
                        totalLoad += kvp.Value;
                    }
                    _avgLaneLoad = totalLoad / laneVehicleCounts.Count;
                    // Penalize at 4+ cars per lane
                    _utilizationScore = (_maxLaneLoad <= 4f) ? 100f : Mathf.Max(0, 100f - (_maxLaneLoad - 4f) * 15f);
                }
                else
                {
                    _utilizationScore = 100f;
                }
                
                // === 8. carpark queue score
                _totalCarparkQueue = carparkQueueTotal;
                _carparkScore = (_totalCarparkQueue <= 2) ? 100f : Mathf.Max(0, 100f - (_totalCarparkQueue - 2) * 8f);
                
                // === 9. network stability (repathing) score
                _repathingCars = repathingCount;
                _networkStabilityScore = (_repathingCars <= 2) ? 100f : Mathf.Max(0, 100f - (_repathingCars - 2) * 10f);
                
                // === combine all factors ===
                // critical factors (timer, delivery, service) = 70% of score
                // secondary factors = 30%
                _overallEfficiency = 
                    (_trendScore * 0.25f) +         // critical: timer trend
                    (_deliveryScore * 0.25f) +      // critical: pin delivery
                    (_serviceRateScore * 0.20f) +   // critical: service rate
                    (_stabilityScore * 0.05f) +     // minor: timer stability
                    (_tripScore * 0.05f) +          // minor: trip efficiency
                    (_waitTimeScore * 0.05f) +      // minor: wait time
                    (_utilizationScore * 0.05f) +   // minor: lane utilization
                    (_carparkScore * 0.05f) +       // minor: carpark queue
                    (_networkStabilityScore * 0.05f); // minor: network stability
                
                _overallEfficiency = Mathf.Clamp(_overallEfficiency, 0f, 100f);
                
                // get game's own efficiency score
                var scoreModel = city.Scope.Get<ScoreModel>();
                if (scoreModel != null)
                {
                    _gameEfficiencyScore = (float)scoreModel.EfficiencyScore;
                }
            }
            
            // display our efficiency
            if (_showEfficiency)
            {
                _efficiencyLineIndex = _displayLines.Count;
                string effIcon = (_overallEfficiency >= 70f) ? "OK" : ((_overallEfficiency >= 40f) ? "WARN" : "BAD");
                _displayLines.Add($"Efficiency: {_overallEfficiency:F0}% {effIcon}");
            }
            else
            {
                _efficiencyLineIndex = -1;
            }
            
            // display game's efficiency score
            if (_showGameEfficiency)
            {
                _gameEfficiencyLineIndex = _displayLines.Count;
                _displayLines.Add($"Game Eff: {_gameEfficiencyScore:F0}");
            }
            else
            {
                _gameEfficiencyLineIndex = -1;
            }

            // --- week / upgrade timer ---
            int currentWeek = clock.Week;
            int currentDay = clock.Day % 7;
            
            Fix64 secondsPerWeek = (Fix64)ClockModel.SecondsPerWeek; 
            Fix64 weekProgress = clock.Time % secondsPerWeek;
            Fix64 timeToUpgrade = secondsPerWeek - weekProgress;
            
            string[] visibleDays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            string dayName = (currentDay >= 0 && currentDay < 7) ? visibleDays[currentDay] : "Day" + currentDay;
            
            _displayLines.Add($"Week {currentWeek + 1}: {dayName} ({(float)timeToUpgrade:F0}s)");
        }
        catch (Exception ex)
        {
            _displayLines.Clear();
            _displayLines.Add("Error:");
            _displayLines.Add(ex.Message.Substring(0, Math.Min(40, ex.Message.Length)));
            Debug.LogError($"[RunINFO] Error: {ex}");
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;
        if (_displayLines.Count == 0) return;

        // initialize styles once
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));
        }
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 14;
            _labelStyle.normal.textColor = Color.white;
        }

        // use fixed positioning instead of GUILayout
        float padding = 15f; // increased from 10
        float lineHeight = 20f; // increased from 18
        float startX = 10f;
        float startY = 10f;
        
        // calculate box size - start from 0 and measure all content
        float maxWidth = 150f; // start with decent minimum
        
        // measure title
        Vector2 titleSize = _labelStyle.CalcSize(new GUIContent("▶ RunINFO"));
        if (titleSize.x > maxWidth) maxWidth = titleSize.x;
        
        // measure each line
        foreach (var line in _displayLines)
        {
            Vector2 size = _labelStyle.CalcSize(new GUIContent(line));
            if (size.x > maxWidth)
            {
                maxWidth = size.x;
            }
        }
        
        // add generous margin to prevent any clipping
        maxWidth += 40f; // increased from 20f
        
        float boxWidth = maxWidth + (padding * 2);
        float boxHeight = ((_displayLines.Count + 1) * lineHeight) + (padding * 2);
        
        // draw box
        Rect boxRect = new Rect(startX, startY, boxWidth, boxHeight);
        GUI.Box(boxRect, new GUIContent(""), _boxStyle);
        
        // draw title
        float currentY = startY + padding;
        GUI.Label(new Rect(startX + padding, currentY, maxWidth, lineHeight), "▶ RunINFO", _labelStyle);
        currentY += lineHeight;
        
        // draw each line
        for (int i = 0; i < _displayLines.Count; i++)
        {
            var line = _displayLines[i];
            Color lineColor = Color.white;
            
            if (i == _supplyLineIndex)
            {
                lineColor = GetSupplyColor(_supplyPercent);
            }
            else if (i == _trafficLineIndex)
            {
                lineColor = GetFlowColor(_trafficFlowPercent, _stuckCarCount);
            }
            else if (i == _overcrowdLineIndex)
            {
                lineColor = GetOvercrowdColor(_overcrowdPercent);
            }
            else if (i == _spawnRampLineIndex)
            {
                lineColor = GetSpawnRampColor(_spawnRampValue);
            }
            else if (i == _efficiencyLineIndex)
            {
                lineColor = GetEfficiencyColor(_overallEfficiency);
            }
            
            GUIStyle coloredStyle = new GUIStyle(_labelStyle);
            coloredStyle.normal.textColor = lineColor;
            GUI.Label(new Rect(startX + padding, currentY, maxWidth, lineHeight), line, coloredStyle);
            
            currentY += lineHeight;
        }
    }

    private Color GetSupplyColor(float percent)
    {
        if (percent >= 100f)
        {
            return Color.white; 
        }
        else if (percent >= 80f)
        {
            float t = (100f - percent) / 20f; 
            return Color.Lerp(Color.white, Color.yellow, t);
        }
        else if (percent >= 50f)
        {
            float t = (80f - percent) / 30f; 
            return Color.Lerp(Color.yellow, Color.red, t);
        }
        else
        {
            return Color.red; 
        }
    }

    private Color GetFlowColor(float percent, int stuckCount)
    {
        if (stuckCount >= 5) return Color.red; 
        if (stuckCount > 0) return Color.Lerp(Color.yellow, Color.red, stuckCount / 5f);
        
        if (percent >= 80f) return Color.green;
        if (percent >= 50f) return Color.yellow;
        return Color.red;
    }
    
    private Color GetOvercrowdColor(float percent)
    {
        if (percent >= 90f) return Color.red; 
        if (percent >= 70f) return Color.Lerp(Color.yellow, Color.red, (percent - 70f) / 20f);
        if (percent >= 50f) return Color.yellow;
        return new Color(1f, 0.5f, 0f); 
    }
    
    private Color GetSpawnRampColor(float ramp)
    {
        if (ramp >= 2.0f) return Color.red;
        if (ramp >= 1.5f) return Color.Lerp(Color.yellow, Color.red, (ramp - 1.5f) / 0.5f);
        if (ramp >= 1.2f) return Color.yellow;
        return Color.white; 
    }
    
    private Color GetEfficiencyColor(float efficiency)
    {
        if (efficiency >= 80f) return Color.green;
        if (efficiency >= 50f) return Color.Lerp(Color.yellow, Color.green, (efficiency - 50f) / 30f);
        if (efficiency >= 25f) return Color.Lerp(Color.red, Color.yellow, (efficiency - 25f) / 25f);
        return Color.red;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
