using TMPro;
using UnityEngine.UI;
using System;

namespace BombGame.UI
{
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Ryuwen Stats Reference")]
        [SerializeField] private StatsController _stats;
        [SerializeField] private Animator _animator;

        private void OnEnable()
        {
            _stats.OnHpChange += OnHpChange;
            _stats.OnBombChange += OnBombChange;
            _stats.OnSpeedChange += OnSpeedChange;
            _stats.OnExpoleChange += OnExpoleChange;
        }

        private void OnDisable()
        {
            _stats.OnHpChange -= OnHpChange;
            _stats.OnBombChange -= OnBombChange;
            _stats.OnSpeedChange -= OnSpeedChange;
            _stats.OnExpoleChange -= OnExpoleChange;
        }

        private void OnHpChange(int hp)
        {
            _animator.SetInteger("Hp",hp);
        }

        private void OnBombChange(int bomb)
        {
            _animator.SetInteger("Bomb", bomb);
        }

        private void OnSpeedChange(int speed)
        {
            _animator.SetInteger("Speed", speed);
        }

        private void OnExpoleChange(int expole)
        {
            _animator.SetInteger("Expole", expole);
        }
    }
}