namespace RoomBattle.Health
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using RoomBattle.Common;
    using RoomBattle.Health.Damage;
    using RoomBattle.Logging;

    using UnityEngine;

    public class Health : MonoBehaviour, IDamageable, IPausable
    {
        [SerializeField] private uint _maxHealthCount = 100;
        private List<Coroutine> _addingMaxHealthCoroutines;
        private bool _isPaused;

        public float HealthCount { get; private set; }

        public uint MaxHealth => _maxHealthCount;

        public bool IsOutOfHealth { get; private set; }

        public bool TakeDamage(Damage.Damage damage, out string logMessage)
        {
            if (damage.WasUsed)
            {
                logMessage = "Damage instance was already used!";
                return false;
            }

            if (HealthCount == 0)
            {
                logMessage = "Health is already 0!";
                return false;
            }

            if (damage.Value <= 0)
            {
                logMessage = "Damage value is 0!";
                return false;
            }

            logMessage = "Successfully applied damage!";
            var oldHealth = HealthCount;

            if (damage.Value > HealthCount)
            {
                HealthCount = 0;
                HealthDecreased?.Invoke(this, new HealthChangedEventArgs(oldHealth, HealthCount));
                RaiseOutOfHealth();
                return true;
            }

            HealthCount -= damage.Value;
            HealthDecreased?.Invoke(this, new HealthChangedEventArgs(oldHealth, HealthCount));
            if (HealthCount == 0)
                RaiseOutOfHealth();
            return true;
        }

        public string Name => gameObject.name;

        public event EventHandler HealthRestored;

        public event EventHandler MaxHealthDecreased;

        public event EventHandler MaxHealthIncreased;

        public event EventHandler OutOfHealth;

        public event EventHandler<HealthChangedEventArgs> HealthDecreased;

        public event EventHandler<HealthChangedEventArgs> HealthIncreased;

        public void AddMaxHealthBuff(ConstantBuff maxHealthBuff)
        {
            maxHealthBuff.Enabled += OnBuffEnableChanged;
            if (!maxHealthBuff.IsEnabled)
                return;

            IncreaseMaxHealth(maxHealthBuff.Value);
        }

        public void TakeHeal(Heal heal)
        {
            if (heal.WasUsed)
            {
                MLog.Error("Heal instance was already used!");
                return;
            }

            if (heal.Value <= 0)
            {
                MLog.Error("Heal instance had 0 heal inside!");
                return;
            }

            var possibleHealthAddition = _maxHealthCount - HealthCount;
            float healthToAdd = heal.Value;

            if (possibleHealthAddition <= 0)
                return;

            if (healthToAdd >= possibleHealthAddition)
                healthToAdd = possibleHealthAddition;

            //TODO: Dirty fucking callback hack. But it works.
            var callbackContainer = new CallbackContainer();
            var coroutine = StartCoroutine(StartAddingHealth(healthToAdd, callbackContainer));
            callbackContainer.Callback = () => _addingMaxHealthCoroutines.Remove(coroutine);
            _addingMaxHealthCoroutines.Add(coroutine);
        }

        private void StopAllHealing()
        {
            foreach (var routine in _addingMaxHealthCoroutines)
            {
                if (routine != null)
                    StopCoroutine(routine);
            }
            _addingMaxHealthCoroutines.Clear();
        }

        private const float PRECISION = 0.001f;

        private class CallbackContainer
        {
            public Action Callback { get; set; }
        }
        private IEnumerator StartAddingHealth(float healthToAddTotal, CallbackContainer container)
        {
            //var healthAdditionStep = healthToAddTotal / 
            const float additionDurationSeconds = 1f;
            var current = 0f;
            const float step = additionDurationSeconds / 20;
            var wasHealthZero = Mathf.Abs(HealthCount) < PRECISION;
            while (current < additionDurationSeconds)
            {
                while (_isPaused)
                {
                    yield return null;
                }
                current += step;
                var healthAddition = healthToAddTotal * step;
                var possibleHealthAddition = _maxHealthCount - HealthCount;
                if (possibleHealthAddition <= 0)
                {
                    yield return null;
                    container.Callback?.Invoke();
                    yield break;
                }

                if (healthAddition >= possibleHealthAddition)
                    healthAddition = possibleHealthAddition;

                var oldHealth = HealthCount;
                HealthCount += healthAddition;
                HealthIncreased?.Invoke(this, new HealthChangedEventArgs(oldHealth, HealthCount));
                if (wasHealthZero)
                {
                    HealthRestored?.Invoke(this, EventArgs.Empty);
                    wasHealthZero = false;
                }

                var cur = 0f;
                while (cur < step)
                {
                    if (_isPaused)
                    {
                        yield return null;
                        continue;
                    }
                    cur += Time.deltaTime;
                    yield return null;
                }
            }
            container.Callback?.Invoke();
        }

        private void Awake()
        {
            _addingMaxHealthCoroutines = new List<Coroutine>();
            HealthCount = MaxHealth;
            IsOutOfHealth = false;
            if (HealthCount == 0)
                RaiseOutOfHealth();
        }

        private void DecreaseMaxHealth(uint value)
        {
            var currentHp = HealthCount;
            var currentMaxHp = _maxHealthCount;
            _maxHealthCount -= value;

            var newCurrentHp = currentHp * _maxHealthCount / currentMaxHp;
            HealthCount = (uint)newCurrentHp;

            if (HealthCount > _maxHealthCount)
                HealthCount = _maxHealthCount;

            MaxHealthDecreased?.Invoke(this, EventArgs.Empty);
            HealthDecreased?.Invoke(this, new HealthChangedEventArgs(currentHp, HealthCount));
        }

        private void IncreaseMaxHealth(uint value)
        {
            var currentHp = HealthCount;
            var currentMaxHp = _maxHealthCount;
            _maxHealthCount += value;

            var newCurrentHp = currentHp * _maxHealthCount / currentMaxHp;
            HealthCount = (uint)newCurrentHp;

            MaxHealthIncreased?.Invoke(this, EventArgs.Empty);
            HealthIncreased?.Invoke(this, new HealthChangedEventArgs(currentHp, HealthCount));
        }

        private void OnBuffEnableChanged(object sender, bool isEnabled)
        {
            var buff = (ConstantBuff)sender;
            if (!isEnabled)
            {
                DecreaseMaxHealth(buff.Value);
                return;
            }

            IncreaseMaxHealth(buff.Value);
        }

        private void OnDisable()
        {
            OutOfHealth -= OnOutOfHealth;
            HealthRestored -= OnHealthRestored;
        }

        private void OnEnable()
        {
            OutOfHealth += OnOutOfHealth;
            HealthRestored += OnHealthRestored;
        }

        private void OnHealthRestored(object sender, EventArgs e)
        {
            IsOutOfHealth = false;
        }

        private void OnOutOfHealth(object sender, EventArgs e)
        {
            IsOutOfHealth = true;
            StopAllHealing();
        }

        private void RaiseOutOfHealth()
        {
            OutOfHealth?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }
    }
}
