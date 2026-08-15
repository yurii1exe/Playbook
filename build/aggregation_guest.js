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
      _id: "$TGuest.Name",
      // firstHalf: 1,
      // secondHalf: 1,
      // totalScored: 1,
      attacksTotal: {
        $sum: "$TGuest.Stats0.Attacks",
      },
      attacksAvg: {
        $avg: "$TGuest.Stats0.Attacks",
      },
      dangerousAttacksTotal: {
        $sum: "$TGuest.Stats0.DangerousAttacks",
      },
      dangerousAttacksAvg: {
        $avg: "$TGuest.Stats0.DangerousAttacks",
      },
      goalAttemptsTotal: {
        $sum: "$TGuest.Stats0.GoalAttempts",
      },
      goalAttemptsAvg: {
        $avg: "$TGuest.Stats0.GoalAttempts",
      },
      shotsOnGoalTotal: {
        $sum: "$TGuest.Stats0.ShotsOnGoal",
      },
      shotsOnGoalAvg: {
        $avg: "$TGuest.Stats0.ShotsOnGoal",
      },
      shotsOffGoalTotal: {
        $sum: "$TGuest.Stats0.ShotsOffGoal",
      },
      shotsOffGoalAvg: {
        $avg: "$TGuest.Stats0.ShotsOffGoal",
      },
      blockedShotsTotal: {
        $sum: "$TGuest.Stats0.BlockedShots",
      },
      blockedShotsAvg: {
        $avg: "$TGuest.Stats0.BlockedShots",
      },
      freeKicksTotal: {
        $sum: "$TGuest.Stats0.FreeKicks",
      },
      freeKicksAvg: {
        $avg: "$TGuest.Stats0.FreeKicks",
      },
      cornerKicksTotal: {
        $sum: "$TGuest.Stats0.CornerKicks",
      },
      cornerKicksAvg: {
        $avg: "$TGuest.Stats0.CornerKicks",
      },
      offsidesTotal: {
        $sum: "$TGuest.Stats0.Offsides",
      },
      offsidesAvg: {
        $avg: "$TGuest.Stats0.Offsides",
      },
      throwInTotal: {
        $sum: "$TGuest.Stats0.ThrowIn",
      },
      throwInAvg: {
        $avg: "$TGuest.Stats0.ThrowIn",
      },
      foulsTotal: {
        $sum: "$TGuest.Stats0.Fouls",
      },
      foulsAvg: {
        $avg: "$TGuest.Stats0.Fouls",
      },
      completedPassesTotal: {
        $sum: "$TGuest.Stats0.CompletedPasses",
      },
      completedPassesAvg: {
        $avg: "$TGuest.Stats0.CompletedPasses",
      },
      totalPassesTotal: {
        $sum: "$TGuest.Stats0.TotalPasses",
      },
      totalPassesAvg: {
        $avg: "$TGuest.Stats0.TotalPasses",
      },
      ballPossessionTotal: {
        $sum: "$TGuest.Stats0.BallPossession",
      },
      ballPossessionAvg: {
        $avg: "$TGuest.Stats0.BallPossession",
      },
      yellowCardsTotal: {
        $sum: "$TGuest.Stats0.YellowCards",
      },
      yellowCardsAvg: {
        $avg: "$TGuest.Stats0.YellowCards",
      },
      expectedGoalsTotal: {
        $sum: "$TGuest.Stats0.ExpectedGoals",
      },
      expectedGoalsAvg: {
        $avg: "$TGuest.Stats0.ExpectedGoals",
      },
      goalsFirstHalfTotal: {
        $sum: "$TGuest.GoalsPerFirst",
      },
      goalsFirstHalfAvg: {
        $avg: "$TGuest.GoalsPerFirst",
      },
    },
  },
  {
    $sort: { goalsFirstHalfTotal: -1 },
  },
]