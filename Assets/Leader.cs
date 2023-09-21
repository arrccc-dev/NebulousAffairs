#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;
using static TurnHandler;

public class Leader {

    #region Fields
    private string name;
    // Currency stockpiles, representing the current unspent currency value.
    private int affluenceStockpile;
    private int politicsStockpile;
    private int intelligenceStockpile;
    // Currency yields, represented as a per-turn integer.
    private int affluenceYield;
    private int politicsYield;
    private int intelligenceYield;
    // Relationships and influence.
    private Dictionary<string, Relationship> relationships; // (Leader Name) -> (Leader Relationship)
    private Dictionary<string, Influence> influences; // (Planet Name) -> (Planet Relationship)
    private Dictionary<string, Planet> controlledPlanets;
    private int planetControlCount;
    // Personality
    private LeaderDecisionProfile decisionProfile;

    #endregion

    #region Properties
    public string Name => name;
    public int AffluenceStockpile => affluenceStockpile;
    public int PoliticsStockpile => politicsStockpile;
    public int IntelligenceStockpile => intelligenceStockpile;

    public int AffluenceYield => affluenceYield;
    public int PoliticsYield => politicsYield;
    public int IntelligenceYield => intelligenceYield;
    public LeaderDecisionProfile DecisionProfile => decisionProfile;
    public int PlanetControlCount => planetControlCount;
    #endregion

    #region Events
    public delegate void TurnChangeHandler(GameTurns gameTurns);
    public event TurnChangeHandler? TurnChanged;
    #endregion

    #region Constructors & Builders
    public Leader(string name, float hoarder, float loner, float affluenceBias, float politicsBias, float intelligenceBias,
        int startingAffluence = 0, int startingPolitics = 0, int startingIntelligence = 0, int affluenceYield = 1,
        int politicsYield = 1, int intelligenceYield = 1) {
        this.affluenceYield = affluenceYield;
        this.politicsYield = politicsYield;
        this.intelligenceYield = intelligenceYield;

        planetControlCount = 0; // To be 'corrected' to 2 when building connections in GameHandler
        this.name = name;
        affluenceStockpile = startingAffluence;
        politicsStockpile = startingPolitics;
        intelligenceStockpile = startingIntelligence;

        influences = new();
        controlledPlanets = new();
        decisionProfile = new(this, hoarder, loner, affluenceBias, politicsBias, intelligenceBias); // TODO: Make a builder class for this!
    }

    public void AddNewInfluence(Influence influence) {
        influences.Add(influence.PlanetName, influence);
        decisionProfile.AddNewPlanet(influence);
    }
    #endregion

    #region Getters/Setters

    public List<Influence> GetAscendingSortedPlanetInfluences() {
        List<Influence> sortedPlanetInfluences = new();
        foreach (Influence influence in influences.Values) {
            sortedPlanetInfluences.Add(influence);
        }
        sortedPlanetInfluences.Sort((influence1, influence2) => influence1.InfluenceValue.CompareTo(influence2.InfluenceValue));
        return sortedPlanetInfluences;
    }

    public List<Planet> GetControlledPlanets() {
        List<Planet> controlledPlanetsList = new();
        foreach (Planet controlledPlanet in controlledPlanets.Values) {
            controlledPlanetsList.Add(controlledPlanet);
        }
        return controlledPlanetsList;
    }

    public void SetAffluenceStockpile(int affluenceStockpile) {
        this.affluenceStockpile = affluenceStockpile;
    }

    public void SetPoliticsStockpile(int affluenceStockpile) {
        this.affluenceStockpile = affluenceStockpile;
    }

    public void SetIntelligenceStockpile(int affluenceStockpile) {
        this.affluenceStockpile = affluenceStockpile;
    }

    public void SetAffluenceYield(int affluenceYield) { 
        this.affluenceYield = affluenceYield;
    }

    public void SetPoliticsYields(int politicsYield) { 
        this.politicsYield = politicsYield;
    }

    public void SetIntelligenceYields(int intelligenceYield) { 
        this.intelligenceYield = intelligenceYield;
    }

    public void SetPlanetInfluence(string planetName, float leaderInfluence) {
        if (!influences.ContainsKey(planetName)) {
            Debug.LogError("Leader.SetPlanetInfluence tried to access a planet by name that did not exist in the influences Dictionary.");
        }
        influences[planetName].SetInfluence(leaderInfluence);
    }

    public Influence GetPlanetInfluence(string planetName) {
        return influences[planetName];
    }
    #endregion

    #region Game Actions
    public GameAction MakeDecision() {
        GameAction action = decisionProfile.ChooseNextAction(4);
        return action;
    }

    public void UpdatePriorities() {
        decisionProfile.UpdatePriorities();
    }

    /// <summary>
    /// This method does nothing if this leader was already the leader of the planet.
    /// </summary>
    /// <param name="planet"></param>
    public void GainPlanetControl(Planet planet) {
        bool wasLeader = influences[planet.Name].SetIsLeader(true);
        if (!wasLeader) {
            controlledPlanets.Add(planet.Name, planet);
            planetControlCount++;
        }
    }

    public void LosePlanetControl(Planet planet) {
        bool wasLeader = influences[planet.Name].SetIsLeader(false);
        if (wasLeader) {
            controlledPlanets.Remove(planet.Name, out _);
            planetControlCount--;
        }
    }
    #endregion

    public class LeaderDecisionProfile {
        private const int ComfortableAffluenceSurplus = 20;
        private const int ComfortablePoliticsSurplus = 25;
        private const int ComfortableIntelligenceSurplus = 25;
        private const int ComfortableAffluenceYield = 2;
        private const int ComfortablePoliticsYield = 2;
        private const int ComfortableIntelligenceYield = 2;

        private const int ActionsToChooseFrom = 2;
        private const int PlanetsToChooseFrom = 4;

        private Leader leader;
        private System.Random random;
        // Personality-driven static influences
        private float hoarder; // 0 = values high yields; 1 = values high stockpiles
        private float loner; // 0 = values solo actions; 1 = values leader interactions
        // Static decision influencers
        private float affluenceBias;
        private float politicsBias;
        private float intelligenceBias;
        // Dynamic decision influencers
        private float affluencePriority;
        private float politicsPriority;
        private float intelligencePriority;
        private List<Influence> planetPriorities;

        public float AffluenceRawBias => affluenceBias;
        public float PoliticsRawBias => politicsBias;
        public float IntellectRawBias => intelligenceBias;

        public float AffluencePriority => affluencePriority;
        public float PoliticsPriority => politicsPriority;
        public float IntelligencePriority => intelligencePriority;
        public List<Influence> PlanetPriorities => planetPriorities;

        public LeaderDecisionProfile(Leader leader, float hoarder, float loner, float affluenceBias, float politicsBias, float intelligenceBias) {
            this.random = new();
            this.leader = leader;
            this.hoarder = hoarder;
            this.loner = loner;
            this.affluenceBias = affluenceBias;
            this.politicsBias = politicsBias;
            this.intelligenceBias = intelligenceBias;

            planetPriorities = new();
        }

        public void AddNewPlanet(Influence influence) {
            planetPriorities.Add(influence);
        }

        public GameAction ChooseNextAction(int actionsToConsider) {
            List<GameAction> possibleActions = GetLikelyActions(actionsToConsider);
            return possibleActions[random.Next(possibleActions.Count)];
        }

        public List<GameAction> GetLikelyActions(int actionsToGet) {
            List<GameAction> likelyActions = new();

            (ActionTypes, float) espionageAction = (ActionTypes.EspionageAction, TradeAction.ComputeActionDecisionWeight(leader));
            (ActionTypes, float) diplomacyAction = (ActionTypes.EspionageAction, DiplomacyAction.ComputeActionDecisionWeight(leader));
            (ActionTypes, float) tradeAction = (ActionTypes.EspionageAction, TradeAction.ComputeActionDecisionWeight(leader));

            List<(ActionTypes, float)> generalActionWeights = new() { espionageAction, diplomacyAction, tradeAction};
            generalActionWeights.Sort((action1, action2) => action1.Item2.CompareTo(action2.Item2));
            int actionsPerCategory = actionsToGet / ActionsToChooseFrom;
            ActionTypes highestPriorityGeneralAction = generalActionWeights[0].Item1;

            for (int i = 0; i < ActionsToChooseFrom; i++) {
                ActionTypes currencyActionType = generalActionWeights[i].Item1;
                PopulateDecisionList(likelyActions, currencyActionType, actionsPerCategory);
            }

            return likelyActions;
        }

        public void PopulateDecisionList(List<GameAction> gameActionList, ActionTypes gameActionType, int numOfActions) {
            switch (gameActionType) { 
                case ActionTypes.EspionageAction:
                    goto case ActionTypes.DiplomacyAction;
                case ActionTypes.DiplomacyAction:
                    for (int i = 0; i < numOfActions; i++) {
                        Array currencies = Enum.GetValues(typeof(CurrencyType));
                        int randomCurrencyIndex = random.Next(currencies.Length);
                        CurrencyType currencyToIncrease = (CurrencyType) currencies.GetValue(randomCurrencyIndex); // TODO: THIS IS STUPID MAKE THIS SMARTER!!
                        int nextCurrencyIndex = (randomCurrencyIndex + 1) % currencies.Length;
                        CurrencyType currencyToDecrease = (CurrencyType) currencies.GetValue(nextCurrencyIndex);
                        Planet targetPlanet = planetPriorities[i + random.Next(PlanetsToChooseFrom)].Planet;
                        gameActionList.Add(new DiplomacyAction(0, leader, targetPlanet, currencyToIncrease, currencyToDecrease));
                    }
                    break;
                case ActionTypes.TradeAction:
                    goto case ActionTypes.DiplomacyAction;
            }
        }

        public void UpdatePriorities() {
            UpdateAffluencePriority();
            UpdatePoliticsPriority();
            UpdateIntelligencePriority();
            UpdatePlanetPriorities();
        }

        private void UpdateAffluencePriority() {
            float surplusFactor = 1 - (Mathf.Clamp(leader.affluenceStockpile, 0, 1_000) / (ComfortableAffluenceSurplus * 2 * hoarder)); // Calculate actual theoretical maximums
            float yieldFactor = 1 - (Mathf.Clamp(leader.affluenceYield, 0, 1_000) / (ComfortableAffluenceYield * 2 * (1 - hoarder)));
            float sumFactors = Mathf.Clamp(surplusFactor, 0, 1) + Mathf.Clamp(yieldFactor, 0, 1);
            affluencePriority = Mathf.Clamp(sumFactors, 0, 1);
        }

        private void UpdatePoliticsPriority() {
            float surplusFactor = 1 - (Mathf.Clamp(leader.politicsStockpile, 0, 1_000) / (ComfortablePoliticsSurplus * 2 * hoarder)); // Calculate actual theoretical maximums
            float yieldFactor = 1 - (Mathf.Clamp(leader.politicsYield, 0, 1_000) / (ComfortablePoliticsYield * 2 * (1 - hoarder)));
            float sumFactors = Mathf.Clamp(surplusFactor, 0, 1) + Mathf.Clamp(yieldFactor, 0, 1);
            politicsPriority = Mathf.Clamp(sumFactors, 0, 1);
        }

        private void UpdateIntelligencePriority() {
            float surplusFactor = 1 - (Mathf.Clamp(leader.intelligenceStockpile, 0, 1_000) / (ComfortableIntelligenceSurplus * 2 * hoarder)); // Calculate actual theoretical maximums
            float yieldFactor = 1 - (Mathf.Clamp(leader.intelligenceYield, 0, 1_000) / (ComfortableIntelligenceYield * 2 * (1 - hoarder)));
            float sumFactors = Mathf.Clamp(surplusFactor, 0, 1) + Mathf.Clamp(yieldFactor, 0, 1);
            intelligencePriority = Mathf.Clamp(sumFactors, 0, 1);
        }

        private void UpdatePlanetPriorities() {
            planetPriorities = leader.GetAscendingSortedPlanetInfluences();
        }
    }
}

public class Relationship {
    #region Fields
    private Leader originLeader;
    private Leader targetLeader;
    private float opinionValue;
    #endregion

    #region Properties
    public string TargetLeaderName => targetLeader.Name;
    public float OpinionValue => opinionValue;
    #endregion

    #region Constructors
    public Relationship(Leader originLeader, Leader targetLeader, float opinionValue = 0) {
        this.originLeader = originLeader;
        this.targetLeader = targetLeader;
        this.opinionValue = opinionValue;
    }
    #endregion

    // public void UpdateRelationship(Action)
}

public class Influence {
    #region Fields
    private Leader leader;
    private Planet planet;
    private float influenceValue;
    private bool isLeader;
    #endregion

    #region Properties
    public string LeaderName => leader.Name;
    public string PlanetName => planet.Name;
    public Planet Planet => planet;
    public Leader Leader => leader;
    public float InfluenceValue => influenceValue;
    public bool IsLeader => isLeader;
    #endregion

    #region Constructors
    public Influence(Leader leader, Planet planet, float influenceValue = 0, bool isLeader = false) {
        this.leader = leader;
        this.planet = planet;
        this.influenceValue = influenceValue;
        this.isLeader = isLeader;
    }
    #endregion

    public void SetInfluence(float influenceValue) {
        this.influenceValue = influenceValue;
    }

    public void UpdateInfluence(float influenceModifier) {
        float newInfluenceValue = Mathf.Clamp(influenceValue + influenceModifier, 0, 1);
        influenceValue = newInfluenceValue;
    }
    /// <summary>
    /// Returns true if this leader was already the leader of the planet.
    /// </summary>
    public bool SetIsLeader(bool isLeader) {
        bool previousIsLeader = this.isLeader;
        this.isLeader = isLeader;
        return previousIsLeader;
    }
}