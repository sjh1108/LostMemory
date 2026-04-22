using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public class TestKhiInteractionZone : ButtonActivatedZone
    {
        [SerializeField] private string interactionLabel = "TestKhi Interaction Test";
        [SerializeField] private Color idleColor = new Color(0.2f, 0.55f, 0.95f, 0.85f);
        [SerializeField] private Color readyColor = new Color(0.25f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private Color activatedColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float activationFlashDuration = 0.25f;
        [SerializeField] private TestKhiDoorAction doorAction;

        private SpriteRenderer _spriteRenderer;
        private int _activationCount;
        private Coroutine _flashCoroutine;
        private Character _activeCharacter;
        private readonly Dictionary<GameObject, int> _colliderObjectOverlapCounts = new Dictionary<GameObject, int>();
        private readonly Dictionary<Character, int> _characterOverlapCounts = new Dictionary<Character, int>();

        public void ConfigureForTest()
        {
            ButtonActivatedRequirement = ButtonActivatedRequirements.Character;
            RequiresPlayerType = true;
            RequiresButtonActivationAbility = true;
            Activable = true;
            AutoActivation = false;
            CanOnlyActivateIfGrounded = false;
            OnlyOneActivationAtOnce = true;
            UnlimitedActivations = true;
            DelayBetweenUses = 0.1f;
            InputType = InputTypes.Default;
            UseVisualPrompt = false;
            AlwaysShowPrompt = false;
            ShowPromptWhenColliding = false;

            int playerLayer = LayerMask.NameToLayer("Player");
            TargetLayerMask = playerLayer >= 0 ? 1 << playerLayer : ~0;
            doorAction = GetComponent<TestKhiDoorAction>();
        }

        public override void Initialization()
        {
            ConfigureForTest();
            base.Initialization();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            SetColor(idleColor);
        }

        protected override void TriggerEnter(GameObject collider)
        {
            if (collider == null || !CheckConditions(collider))
            {
                return;
            }

            if (_colliderObjectOverlapCounts.TryGetValue(collider, out int overlapCount))
            {
                _colliderObjectOverlapCounts[collider] = overlapCount + 1;
                return;
            }

            _colliderObjectOverlapCounts[collider] = 1;
            base.TriggerEnter(collider);
        }

        protected override void TriggerExit(GameObject collider)
        {
            if (collider == null)
            {
                return;
            }

            if (!_colliderObjectOverlapCounts.TryGetValue(collider, out int overlapCount))
            {
                return;
            }

            overlapCount--;
            if (overlapCount > 0)
            {
                _colliderObjectOverlapCounts[collider] = overlapCount;
                return;
            }

            _colliderObjectOverlapCounts.Remove(collider);
            base.TriggerExit(collider);
        }

        public override void TriggerEnterAction(GameObject collider)
        {
            Character character = ResolveCharacter(collider);
            if (character == null)
            {
                return;
            }

            if (_characterOverlapCounts.TryGetValue(character, out int overlapCount))
            {
                _characterOverlapCounts[character] = overlapCount + 1;
                return;
            }

            if (_activeCharacter != null)
            {
                return;
            }

            _characterOverlapCounts[character] = 1;
            _activeCharacter = character;
            base.TriggerEnterAction(collider);
            SetColor(readyColor);
            Debug.Log($"[TestKhiInteract] Entered '{interactionLabel}'. Press E to interact.");
        }

        public override void TriggerExitAction(GameObject collider)
        {
            Character character = ResolveCharacter(collider);
            if (character == null)
            {
                return;
            }

            if (!_characterOverlapCounts.TryGetValue(character, out int overlapCount))
            {
                return;
            }

            overlapCount--;
            if (overlapCount > 0)
            {
                _characterOverlapCounts[character] = overlapCount;
                return;
            }

            _characterOverlapCounts.Remove(character);

            if (_activeCharacter != character)
            {
                return;
            }

            _activeCharacter = null;
            base.TriggerExitAction(collider);
            SetColor(idleColor);
            Debug.Log($"[TestKhiInteract] Exited '{interactionLabel}'.");
        }

        protected override void ActivateZone()
        {
            base.ActivateZone();
            _activationCount++;
            Debug.Log($"[TestKhiInteract] Activated '{interactionLabel}' count={_activationCount}.");
            ActivateLinkedAction();

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashActivation());
        }

        private void ActivateLinkedAction()
        {
            if (doorAction == null)
            {
                doorAction = GetComponent<TestKhiDoorAction>();
            }

            if (doorAction != null)
            {
                doorAction.Activate();
            }
        }

        private IEnumerator FlashActivation()
        {
            SetColor(activatedColor);
            yield return new WaitForSeconds(activationFlashDuration);
            SetColor(readyColor);
            _flashCoroutine = null;
        }

        private void SetColor(Color color)
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }

        protected override bool CheckConditions(GameObject collider)
        {
            if (!base.CheckConditions(collider))
            {
                return false;
            }

            Character character = ResolveCharacter(collider);
            return character != null && character.PlayerID == "Player1";
        }

        private static Character ResolveCharacter(GameObject collider)
        {
            return collider.GetComponentInParent<Character>();
        }
    }
}
