[
  {
    $addFields: {
      lastRoundPlayed: {
        $max: ["$RoundNr"],
      },
      firstHalf: {
        $sum: [
          "$THome.GoalsPerFirst",
          "$TGuest.GoalsPerFirst",
        ],
      },
      secondHalf: {
        $sum: [
          "$THome.GoalsPerSecond",
          "$TGuest.GoalsPerSecond",
        ],
      },
      totalScored: {
        $sum: [
          "$THome.GoalsPerFirst",
          "$THome.GoalsPerSecond",
          "$TGuest.GoalsPerFirst",
          "$TGuest.GoalsPerSecond",
        ],
      },
      attacksTotal: {
        $sum: "$THome.Stats0.Attacks",
      },
    },
  },
  {
    $match: {
      // firstHalf: { $gte: 2 },
      // totalScored: { $gte: 4 },  
    },
  },
  {
    // $project: {
    $group: {
      _id: "$THome.Name",
      // firstHalf: 1,
      // secondHalf: 1,
      // totalScored: 1,
      attacksTotal: {
        $sum: "$THome.Stats0.Attacks",
      },
      attacksAvg: {
        $avg: "$THome.Stats0.Attacks",
      },
      dangerousAttacksTotal: {
        $sum: "$THome.Stats0.DangerousAttacks",
      },
      dangerousAttacksAvg: {
        $avg: "$THome.Stats0.DangerousAttacks",
      },
      goalAttemptsTotal: {
        $sum: "$THome.Stats0.GoalAttempts",
      },
      goalAttemptsAvg: {
        $avg: "$THome.Stats0.GoalAttempts",
      },
      shotsOnGoalTotal: {
        $sum: "$THome.Stats0.ShotsOnGoal",
      },
      shotsOnGoalAvg: {
        $avg: "$THome.Stats0.ShotsOnGoal",
      },
      shotsOffGoalTotal: {
        $sum: "$THome.Stats0.ShotsOffGoal",
      },
      shotsOffGoalAvg: {
        $avg: "$THome.Stats0.ShotsOffGoal",
      },
      blockedShotsTotal: {
        $sum: "$THome.Stats0.BlockedShots",
      },
      blockedShotsAvg: {
        $avg: "$THome.Stats0.BlockedShots",
      },
      freeKicksTotal: {
        $sum: "$THome.Stats0.FreeKicks",
      },
      freeKicksAvg: {
        $avg: "$THome.Stats0.FreeKicks",
      },
      cornerKicksTotal: {
        $sum: "$THome.Stats0.CornerKicks",
      },
      cornerKicksAvg: {
        $avg: "$THome.Stats0.CornerKicks",
      },
      offsidesTotal: {
        $sum: "$THome.Stats0.Offsides",
      },
      offsidesAvg: {
        $avg: "$THome.Stats0.Offsides",
      },
      throwInTotal: {
        $sum: "$THome.Stats0.ThrowIn",
      },
      throwInAvg: {
        $avg: "$THome.Stats0.ThrowIn",
      },
      foulsTotal: {
        $sum: "$THome.Stats0.Fouls",
      },
      foulsAvg: {
        $avg: "$THome.Stats0.Fouls",
      },
      completedPassesTotal: {
        $sum: "$THome.Stats0.CompletedPasses",
      },
      completedPassesAvg: {
        $avg: "$THome.Stats0.CompletedPasses",
      },
      totalPassesTotal: {
        $sum: "$THome.Stats0.TotalPasses",
      },
      totalPassesAvg: {
        $avg: "$THome.Stats0.TotalPasses",
      },
      ballPossessionTotal: {
        $sum: "$THome.Stats0.BallPossession",
      },
      ballPossessionAvg: {
        $avg: "$THome.Stats0.BallPossession",
      },
      yellowCardsTotal: {
        $sum: "$THome.Stats0.YellowCards",
      },
      yellowCardsAvg: {
        $avg: "$THome.Stats0.YellowCards",
      },
      expectedGoalsTotal: {
        $sum: "$THome.Stats0.ExpectedGoals",
      },
      expectedGoalsAvg: {
        $avg: "$THome.Stats0.ExpectedGoals",
      },
      goalsFirstHalfTotal: {
        $sum: "$THome.GoalsPerFirst",
      },
      goalsFirstHalfAvg: {
        $avg: "$THome.GoalsPerFirst",
      },
    },
  },
  {
    $sort: { goalsFirstHalfTotal: -1 },
  },
]