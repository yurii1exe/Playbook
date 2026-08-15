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
        $sum: "$THome.Stats1.Attacks",
      },
    },
  },
  //filter data
  {
    $match: {
      // firstHalf: { $gte: 2 },
      // totalScored: { $gte: 4 },
    },
  },
  //model functions
  {

    $group: {
      _id: "$THome.Name",
      // firstHalf: 1,
      // secondHalf: 1,
      // totalScored: 1,
      attacksTotal: {
        $sum: "$THome.Stats1.Attacks",
      },
      attacksAvg: {
        $avg: "$THome.Stats1.Attacks",
      },
      dangerousAttacksTotal: {
        $sum: "$THome.Stats1.DangerousAttacks",
      },
      dangerousAttacksAvg: {
        $avg: "$THome.Stats1.DangerousAttacks",
      },
      goalAttemptsTotal: {
        $sum: "$THome.Stats1.GoalAttempts",
      },
      goalAttemptsAvg: {
        $avg: "$THome.Stats1.GoalAttempts",
      },
      shotsOnGoalTotal: {
        $sum: "$THome.Stats1.ShotsOnGoal",
      },
      shotsOnGoalAvg: {
        $avg: "$THome.Stats1.ShotsOnGoal",
      },
      shotsOffGoalTotal: {
        $sum: "$THome.Stats1.ShotsOffGoal",
      },
      shotsOffGoalAvg: {
        $avg: "$THome.Stats1.ShotsOffGoal",
      },
      blockedShotsTotal: {
        $sum: "$THome.Stats1.BlockedShots",
      },
      blockedShotsAvg: {
        $avg: "$THome.Stats1.BlockedShots",
      },
      freeKicksTotal: {
        $sum: "$THome.Stats1.FreeKicks",
      },
      freeKicksAvg: {
        $avg: "$THome.Stats1.FreeKicks",
      },
      cornerKicksTotal: {
        $sum: "$THome.Stats1.CornerKicks",
      },
      cornerKicksAvg: {
        $avg: "$THome.Stats1.CornerKicks",
      },
      offsidesTotal: {
        $sum: "$THome.Stats1.Offsides",
      },
      offsidesAvg: {
        $avg: "$THome.Stats1.Offsides",
      },
      throwInTotal: {
        $sum: "$THome.Stats1.ThrowIn",
      },
      throwInAvg: {
        $avg: "$THome.Stats1.ThrowIn",
      },
      foulsTotal: {
        $sum: "$THome.Stats1.Fouls",
      },
      foulsAvg: {
        $avg: "$THome.Stats1.Fouls",
      },
      completedPassesTotal: {
        $sum: "$THome.Stats1.CompletedPasses",
      },
      completedPassesAvg: {
        $avg: "$THome.Stats1.CompletedPasses",
      },
      totalPassesTotal: {
        $sum: "$THome.Stats1.TotalPasses",
      },
      totalPassesAvg: {
        $avg: "$THome.Stats1.TotalPasses",
      },
      ballPossessionTotal: {
        $sum: "$THome.Stats1.BallPossession",
      },
      ballPossessionAvg: {
        $avg: "$THome.Stats1.BallPossession",
      },
      yellowCardsTotal: {
        $sum: "$THome.Stats1.YellowCards",
      },
      yellowCardsAvg: {
        $avg: "$THome.Stats1.YellowCards",
      },
      expectedGoalsTotal: {
        $sum: "$THome.Stats1.ExpectedGoals",
      },
      expectedGoalsAvg: {
        $avg: "$THome.Stats1.ExpectedGoals",
      },
      goalsFirstHalfTotal: {
        $sum: "$THome.GoalsPerFirst",
      },
      goalsFirstHalfAvg: {
        $avg: "$THome.GoalsPerFirst",
      },
    },
  },
  /// result table
  {
    $project: {
      attacksTotal: 1,
      attacksAvg: 1,
      dangerousAttacksTotal: 1,
      dangerousAttacksAvg: 1,
      goalAttemptsTotal: 1,
      goalAttemptsAvg: 1,
      shotsOnGoalTotal: 1,
      shotsOnGoalAvg: 1,
      shotsOffGoalTotal: 1,
      shotsOffGoalTotal: 1,
      shotsOffGoalAvg: 1,
      blockedShotsTotal: 1,
      blockedShotsAvg: 1,
      freeKicksTotal: 1,
      freeKicksAvg: 1,
      cornerKicksTotal: 1,
      cornerKicksAvg: 1,
      offsidesTotal: 1,
      offsidesAvg: 1,
      throwInTotal: 1,
      throwInAvg: 1,
      foulsTotal: 1,
      foulsAvg: 1,
      completedPassesTotal: 1,
      completedPassesAvg: 1,
      totalPassesTotal: 1,
      totalPassesAvg: 1,
      ballPossessionTotal: 1,
      ballPossessionAvg: 1,
      yellowCardsTotal: 1,
      yellowCardsAvg: 1,
      expectedGoalsTotal: 1,
      expectedGoalsAvg: 1,
      goalsFirstHalfTotal: 1,
      goalsFirstHalfAvg: 1,
      // new functions
      converted_Dangerous: {
        $divide: [
          "$dangerousAttacksTotal",
          "$attacksTotal",
        ],
      },
      goalAtt_DangAtt: {
        $divide: [
          "$goalAttemptsAvg",
          "$dangerousAttacksAvg",
        ],
      },
      shoto_goalAttempts: {
        $divide: [
          "$shotsOnGoalAvg",
          "$goalAttemptsAvg",
        ],
      },
      shotongoal_dangatt: {
        $divide: [
          "$shotsOnGoalAvg",
          "$dangerousAttacksAvg",
        ],
      },
      min45_Dangatt: {
        $divide: [
          45,
          "$dangerousAttacksAvg"
        ],
      },
      failedAttEqatt_Dangatt: {
        $divide: [
          "$attacksAvg",
          "$dangerousAttacksAvg",
        ],
      },
      convertedDangAtt_goalattempt: {
        $divide: [
          "$dangerousAttacksAvg",
          "$goalAttemptsAvg",
        ],
      },
      passes_attacks: {
        $divide: [
          "$completedPassesAvg",
          "$attacksAvg",
        ],
      },
      LowPercFailedAtt_HighPercSHots_DangAtt: {
        $divide: [
          "$failedAttEqatt_Dangatt",
          "$shotongoal_dangatt",
        ],
      },
      shots_time: {
        $divide: [
          45,
          "$goalAttemptsAvg"
        ],
      },
      gksaves_shoton_goals: {
        $subtract: [
          "$shotsOnGoalAvg",
          "$goalsFirstHalfAvg",
        ],
      },
      possesion_att: {
        $divide: [
          "$ballPossessionAvg",
          "$attacksAvg",
        ],
      },
      passes_attacks1: {
        $divide: ["$totalPassesAvg", "$attacksAvg"],
      },
      passes_attacks2: {
        $divide: ["$completedPassesAvg", "$attacksAvg"],
      },
      moreshots_moregoals: {
        $divide: [
          "$goalAttemptsTotal",
          "$goalsFirstHalfTotal",
        ],
      },
      convDangatt_dangAttshots: {
        $divide: [
          "$dangerousAttacksAvg",
          "$goalAttemptsAvg",
        ],
      },
      convAtt_att_shots: {
        $divide: [
          "$attacksAvg",
          "$goalAttemptsAvg",
        ],
      },
      shoton_goals1H: {
        $divide: [
          "$goalsFirstHalfTotal",
          "$shotsOnGoalTotal",
        ],
      },
    },
  },
  ///
  {
    $sort: { goalsFirstHalfTotal: -1 },
  },
]