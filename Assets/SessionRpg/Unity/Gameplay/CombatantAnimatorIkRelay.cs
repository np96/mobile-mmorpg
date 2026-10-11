using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    /// <summary>
    /// Animator IK messages are only sent to components beside the Animator. Combatant
    /// composition keeps its presenter on the simulation root, so this relay forwards
    /// the callback without discovering components during gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CombatantAnimatorIkRelay : MonoBehaviour
    {
        [SerializeField] private CombatantAnimationPresenter _presenter;

        private Animator _animator;

        public CombatantAnimationPresenter Presenter => _presenter;

        public void Bind(CombatantAnimationPresenter presenter)
        {
            _presenter = presenter;
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_presenter != null)
            {
                _presenter.ApplyAnimatorIk(_animator, layerIndex);
            }
        }
    }
}
