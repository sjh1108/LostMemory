using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.AI
{
    public static class EnemyCombatApproachInstaller
    {
        private const string OrcEnemyId = "enemy_orc";
        private const string ChobombEnemyId = "enemy_chobomb";
        private const string MooseEnemyId = "enemy_moose1";
        private const string OrcRiderEnemyId = "enemy_orcrider";
        private const string SkeletonArcherEnemyId = "enemy_skeletonarcher";
        private const string StoneGolemEnemyId = "enemy_stonegolem";
        private const string DetectingStateName = "Detecting";
        private const string IdleStateName = "Idle";
        private const string MovingStateName = "Moving";
        private const string ApproachStateName = "Approach";

        public static void ApplySpawnOverrides(string enemyId, GameObject enemyInstance)
        {
            if (enemyInstance == null || !TryGetProfile(enemyId, out EnemyCombatApproachProfile profile))
            {
                return;
            }

            TryInstallCombatApproach(enemyInstance, profile);
        }

        public static bool TryGetProfile(string enemyId, out EnemyCombatApproachProfile profile)
        {
            switch (enemyId)
            {
                case OrcEnemyId:
                    profile = CreateOrcProfile();
                    return true;
                case ChobombEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        ChobombEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Chobomb",
                        preferredDistance: 1f,
                        distanceTolerance: 0.12f,
                        emergencyRetreatDistance: 0.45f,
                        strafeWeight: 0.2f);
                    return true;
                case MooseEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        MooseEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Moose",
                        preferredDistance: 1.35f,
                        distanceTolerance: 0.15f,
                        emergencyRetreatDistance: 0.75f,
                        strafeWeight: 0.35f);
                    return true;
                case OrcRiderEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        OrcRiderEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Orc Rider",
                        preferredDistance: 4.25f,
                        distanceTolerance: 0.25f,
                        emergencyRetreatDistance: 2.2f,
                        strafeWeight: 0.15f);
                    return true;
                case SkeletonArcherEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        SkeletonArcherEnemyId,
                        ApproachStateName,
                        IdleStateName,
                        "Skeleton Archer",
                        preferredDistance: 5.35f,
                        distanceTolerance: 0.25f,
                        emergencyRetreatDistance: 3.15f,
                        strafeWeight: 0.25f);
                    return true;
                case StoneGolemEnemyId:
                    profile = new EnemyCombatApproachProfile(
                        StoneGolemEnemyId,
                        MovingStateName,
                        DetectingStateName,
                        "Stone Golem",
                        preferredDistance: 1.35f,
                        distanceTolerance: 0.15f,
                        emergencyRetreatDistance: 0.8f,
                        strafeWeight: 0.25f);
                    return true;
                default:
                    profile = default(EnemyCombatApproachProfile);
                    return false;
            }
        }

        public static bool TryGetProfileForPrefabName(string prefabName, out EnemyCombatApproachProfile profile)
        {
            string normalized = prefabName?.ToLowerInvariant() ?? string.Empty;
            if (normalized.Contains("chobomb"))
            {
                return TryGetProfile(ChobombEnemyId, out profile);
            }

            if (normalized.Contains("moose"))
            {
                return TryGetProfile(MooseEnemyId, out profile);
            }

            if (normalized.Contains("orcrider"))
            {
                return TryGetProfile(OrcRiderEnemyId, out profile);
            }

            if (normalized.Contains("skeletonarcher"))
            {
                return TryGetProfile(SkeletonArcherEnemyId, out profile);
            }

            if (normalized.Contains("stonegolem"))
            {
                return TryGetProfile(StoneGolemEnemyId, out profile);
            }

            if (normalized.Contains("orc"))
            {
                return TryGetProfile(OrcEnemyId, out profile);
            }

            profile = default(EnemyCombatApproachProfile);
            return false;
        }

        public static EnemyCombatApproachProfile CreateDefaultMeleeProfile(string label)
        {
            return new EnemyCombatApproachProfile(
                string.Empty,
                MovingStateName,
                DetectingStateName,
                label,
                preferredDistance: 1.2f,
                distanceTolerance: 0.15f,
                emergencyRetreatDistance: 0.65f,
                strafeWeight: 0.3f);
        }

        public static bool TryInstallCombatApproach(GameObject enemyInstance, EnemyCombatApproachProfile profile)
        {
            if (enemyInstance == null)
            {
                return false;
            }

            string stateName = string.IsNullOrEmpty(profile.StateName) ? MovingStateName : profile.StateName;
            string acquisitionStateName = string.IsNullOrEmpty(profile.AcquisitionStateName)
                ? DetectingStateName
                : profile.AcquisitionStateName;
            string label = string.IsNullOrEmpty(profile.Label) ? enemyInstance.name : profile.Label;

            return TryInstallCombatApproach(
                enemyInstance,
                stateName,
                acquisitionStateName,
                label,
                profile.PreferredDistance,
                profile.DistanceTolerance,
                profile.EmergencyRetreatDistance,
                profile.StrafeWeight);
        }

        public static bool TryInstallOrcCombatApproach(
            GameObject enemyInstance,
            string stateName,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            return TryInstallCombatApproach(
                enemyInstance,
                stateName,
                DetectingStateName,
                "Orc",
                preferredDistance,
                distanceTolerance,
                emergencyRetreatDistance,
                strafeWeight);
        }

        private static bool TryInstallCombatApproach(
            GameObject enemyInstance,
            string stateName,
            string acquisitionStateName,
            string label,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            if (enemyInstance == null)
            {
                return false;
            }

            AIBrain brain = enemyInstance.GetComponentInChildren<AIBrain>();
            if (brain == null || brain.States == null)
            {
                return false;
            }

            AIState state = FindState(brain, stateName);
            if (state == null || state.Actions == null)
            {
                return false;
            }

            CharacterMovement movement = enemyInstance.GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
            AIDecisionDetectTargetRadius2D detector = enemyInstance.GetComponentInChildren<AIDecisionDetectTargetRadius2D>();
            LayerMask obstacleMask = detector != null ? detector.ObstacleMask : default(LayerMask);
            bool useObstacleAvoidance = obstacleMask.value != 0;

            if (detector != null)
            {
                detector.ObstacleDetection = false;
            }

            AIDecisionCurrentTargetIsValid keepCurrentTarget = EnsureKeepCurrentTargetDecision(enemyInstance);
            ReplaceDetectionRangeChecksAfterAcquisition(brain, detector, keepCurrentTarget, acquisitionStateName);
            ReplaceTooFarResetChecksAfterAcquisition(brain, keepCurrentTarget, acquisitionStateName);

            int moveActionIndex = FindMoveActionIndex(state);
            if (moveActionIndex < 0)
            {
                return false;
            }

            AIActionCombatApproach2D combatApproach = enemyInstance.GetComponent<AIActionCombatApproach2D>();
            if (combatApproach == null)
            {
                combatApproach = enemyInstance.AddComponent<AIActionCombatApproach2D>();
            }

            combatApproach.Label = $"Runtime {label} Combat Approach";
            combatApproach.Configure(
                movement,
                preferredDistance,
                distanceTolerance,
                emergencyRetreatDistance,
                strafeWeight,
                obstacleMask,
                useObstacleAvoidance);
            combatApproach.Initialization();

            AIActionPathfindToCombatRange2D pathfindToCombatRange =
                enemyInstance.GetComponent<AIActionPathfindToCombatRange2D>();
            if (pathfindToCombatRange == null)
            {
                pathfindToCombatRange = enemyInstance.AddComponent<AIActionPathfindToCombatRange2D>();
            }

            pathfindToCombatRange.Label = $"Runtime {label} Pathfind To Combat Range";
            pathfindToCombatRange.Configure(
                movement,
                combatApproach,
                obstacleMask,
                preferredDistance,
                distanceTolerance);
            pathfindToCombatRange.Initialization();

            state.Actions[moveActionIndex] = pathfindToCombatRange;
            return true;
        }

        private static AIDecisionCurrentTargetIsValid EnsureKeepCurrentTargetDecision(GameObject enemyInstance)
        {
            AIDecisionCurrentTargetIsValid decision = enemyInstance.GetComponent<AIDecisionCurrentTargetIsValid>();
            if (decision == null)
            {
                decision = enemyInstance.AddComponent<AIDecisionCurrentTargetIsValid>();
            }

            decision.Label = "Runtime Keep Current Target";
            decision.Initialization();
            return decision;
        }

        private static void ReplaceDetectionRangeChecksAfterAcquisition(
            AIBrain brain,
            AIDecisionDetectTargetRadius2D detector,
            AIDecisionCurrentTargetIsValid keepCurrentTarget,
            string acquisitionStateName)
        {
            if (brain == null || detector == null || keepCurrentTarget == null || brain.States == null)
            {
                return;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state == null || state.StateName == acquisitionStateName || state.Transitions == null)
                {
                    continue;
                }

                for (int j = 0; j < state.Transitions.Count; j++)
                {
                    AITransition transition = state.Transitions[j];
                    if (transition == null || transition.Decision != detector)
                    {
                        continue;
                    }

                    transition.Decision = keepCurrentTarget;
                }
            }
        }

        private static void ReplaceTooFarResetChecksAfterAcquisition(
            AIBrain brain,
            AIDecisionCurrentTargetIsValid keepCurrentTarget,
            string acquisitionStateName)
        {
            if (brain == null || keepCurrentTarget == null || brain.States == null)
            {
                return;
            }

            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state == null || state.StateName == acquisitionStateName || state.Transitions == null)
                {
                    continue;
                }

                for (int j = 0; j < state.Transitions.Count; j++)
                {
                    AITransition transition = state.Transitions[j];
                    if (!IsTooFarResetTransition(transition, acquisitionStateName))
                    {
                        continue;
                    }

                    string resetState = transition.TrueState;
                    transition.Decision = keepCurrentTarget;
                    transition.TrueState = string.Empty;
                    transition.FalseState = resetState;
                }
            }
        }

        private static bool IsTooFarResetTransition(AITransition transition, string acquisitionStateName)
        {
            if (transition == null
                || string.IsNullOrEmpty(transition.TrueState)
                || !string.IsNullOrEmpty(transition.FalseState))
            {
                return false;
            }

            AIDecisionDistanceToTarget distanceDecision = transition.Decision as AIDecisionDistanceToTarget;
            if (distanceDecision == null)
            {
                return false;
            }

            bool transitionsToReset = transition.TrueState == "Reset" || transition.TrueState == acquisitionStateName;
            if (!transitionsToReset)
            {
                return false;
            }

            return distanceDecision.ComparisonMode == AIDecisionDistanceToTarget.ComparisonModes.GreaterThan
                   || distanceDecision.ComparisonMode == AIDecisionDistanceToTarget.ComparisonModes.StrictlyGreaterThan;
        }

        private static EnemyCombatApproachProfile CreateOrcProfile()
        {
            return new EnemyCombatApproachProfile(
                OrcEnemyId,
                MovingStateName,
                DetectingStateName,
                "Orc",
                preferredDistance: 1f,
                distanceTolerance: 0.1f,
                emergencyRetreatDistance: 0.55f,
                strafeWeight: 0.45f);
        }

        private static AIState FindState(AIBrain brain, string stateName)
        {
            for (int i = 0; i < brain.States.Count; i++)
            {
                AIState state = brain.States[i];
                if (state != null && state.StateName == stateName)
                {
                    return state;
                }
            }

            return null;
        }

        private static int FindMoveActionIndex(AIState state)
        {
            for (int i = 0; i < state.Actions.Count; i++)
            {
                if (state.Actions[i] is AIActionPathfindToCombatRange2D
                    || state.Actions[i] is AIActionCombatApproach2D
                    || state.Actions[i] is AIActionMoveTowardsTarget2D)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public struct EnemyCombatApproachProfile
    {
        public EnemyCombatApproachProfile(
            string enemyId,
            string stateName,
            string acquisitionStateName,
            string label,
            float preferredDistance,
            float distanceTolerance,
            float emergencyRetreatDistance,
            float strafeWeight)
        {
            EnemyId = enemyId;
            StateName = stateName;
            AcquisitionStateName = acquisitionStateName;
            Label = label;
            PreferredDistance = preferredDistance;
            DistanceTolerance = distanceTolerance;
            EmergencyRetreatDistance = emergencyRetreatDistance;
            StrafeWeight = strafeWeight;
        }

        public string EnemyId;
        public string StateName;
        public string AcquisitionStateName;
        public string Label;
        public float PreferredDistance;
        public float DistanceTolerance;
        public float EmergencyRetreatDistance;
        public float StrafeWeight;
    }
}
