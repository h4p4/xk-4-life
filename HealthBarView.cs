namespace RoomBattle.UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using RoomBattle.Common.Animations;
    using RoomBattle.Health;

    using UnityEngine;
    using UnityEngine.UI;

    public class HealthBarView : MonoBehaviour
    {
        [SerializeField] private float _shakeDurationSeconds = 0.4f;
        [SerializeField] private float _shakeScale = 1f;
        [SerializeField] private float _shakeScaleMax = 5f;
        [SerializeField] private float _shakeScaleStep = 0.499f;
        [SerializeField] private GameObject _separatorPrefab;
        [SerializeField] private Health _health;
        [SerializeField] private Slider _damageSlider;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private uint _healthCountBetweenSeparators = 35;
        [SerializeField] private uint _secondsDamageSliderFullShrink = 1;
        [SerializeField] private uint _secondsWithoutDamageToStartShrinkingDamageSlider = 1;
        private Coroutine _countdownToShrinkCoroutine;
        private Coroutine _currentShakeCoroutine;
        private Coroutine _shrinkingCoroutine;
        private float _currentShakeScale;
        private float _secondsSinceDamage;
        private List<GameObject> _currentSeparators;
        private List<Vector3> _shakeDirections;
        private RectTransform _rectTransform;
        private Vector3 _initialPosition;

        private float SecondsWithoutDamageToResetShakeScale => _shakeDurationSeconds;

        private IEnumerator CountdownToShrinkDamageSlider()
        {
            float duration = _secondsWithoutDamageToStartShrinkingDamageSlider;
            float normalizedTime = 0;
            while (normalizedTime <= 1f)
            {
                normalizedTime += Time.unscaledDeltaTime / duration;
                yield return null;
            }

            _shrinkingCoroutine = StartCoroutine(StartShrinkingDamageSlider());
        }

        private IEnumerator ReturnToInitialPos()
        {
            var returnToInitialPosSeconds = 0.07f;
            var currentPos = _rectTransform.localPosition;
            while (returnToInitialPosSeconds > 0)
            {
                returnToInitialPosSeconds -= Time.unscaledDeltaTime;
                var t = returnToInitialPosSeconds / 0.7f;
                _rectTransform.localPosition = Vector3.Lerp(currentPos, _initialPosition, t);
                yield return null;
            }

            _rectTransform.localPosition = _initialPosition;
        }

        private IEnumerator Shake()
        {
            //_rectTransform.localPosition = _initialPosition;

            //if (_rectTransform.localPosition != _initialPosition)
            //    yield return ReturnToInitialPos();

            var oneShakeDuration = _shakeDurationSeconds / _shakeDirections.Count;
            foreach (var shakeDirection in _shakeDirections)
            {
                var currentShakeDuration = oneShakeDuration;

                var initialPos = _initialPosition;
                var shakeDirScaled = new Vector3(shakeDirection.x * _currentShakeScale, shakeDirection.y,
                    shakeDirection.z * _currentShakeScale);
                var shakedPos = initialPos + shakeDirScaled;
                while (currentShakeDuration > 0)
                {
                    currentShakeDuration -= Time.unscaledDeltaTime;
                    var t = currentShakeDuration / oneShakeDuration;
                    _rectTransform.localPosition = Vector3.Lerp(initialPos, shakedPos, t);
                    yield return null;
                }
            }

            yield return ReturnToInitialPos();
        }

        private IEnumerator StartShrinkingDamageSlider()
        {
            var step = 0.01f;
            var wfs = new WaitForSecondsRealtime(step);
            var cur = 0f;
            while (cur <= 1)
            {
                cur += step;
                _damageSlider.value = Mathf.Lerp(_damageSlider.value, _healthSlider.value, cur);
                yield return wfs;
            }

            _damageSlider.value = _healthSlider.value;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnDisable()
        {
            _health.MaxHealthDecreased -= OnMaxHealthChanged;
            _health.MaxHealthIncreased -= OnMaxHealthChanged;
            _health.HealthIncreased -= OnHealthIncreased;
            _health.HealthDecreased -= OnHealthDecreased;
        }

        private void OnEnable()
        {
            _health.MaxHealthDecreased += OnMaxHealthChanged;
            _health.MaxHealthIncreased += OnMaxHealthChanged;
            _health.HealthIncreased += OnHealthIncreased;
            _health.HealthDecreased += OnHealthDecreased;
        }

        private void OnHealthDecreased(object sender, HealthChangedEventArgs e)
        {
            _healthSlider.value = e.NewValue;
            if (_countdownToShrinkCoroutine != null)
            {
                StopCoroutine(_countdownToShrinkCoroutine);
                _countdownToShrinkCoroutine = null;
            }

            if (_shrinkingCoroutine != null)
            {
                StopCoroutine(_shrinkingCoroutine);
                _shrinkingCoroutine = null;
            }

            _countdownToShrinkCoroutine = StartCoroutine(CountdownToShrinkDamageSlider());
            StartShakingAnimation();
            _secondsSinceDamage = 0;
        }

        private void OnHealthIncreased(object sender, HealthChangedEventArgs e)
        {
            _healthSlider.value = e.NewValue;
            if (_damageSlider.value < _healthSlider.value)
                _damageSlider.value = _healthSlider.value;
        }

        private void OnMaxHealthChanged(object sender, EventArgs e)
        {
            var newValue = _health.MaxHealth;
            var scaleChange = _healthSlider.maxValue < newValue ? 0.8f : -0.8f;
            var scaleChangeVector = new Vector2(scaleChange, scaleChange);
            StartCoroutine(SingleAnimation.ScalePop(_rectTransform, 0.4f, scaleChangeVector));

            _healthSlider.maxValue = newValue;
            _damageSlider.maxValue = newValue;
            UpdateSeparators();
        }

        private void Start()
        {
            OnMaxHealthChanged(this, EventArgs.Empty);
            _damageSlider.value = _health.HealthCount;
            _healthSlider.value = _health.HealthCount;

            _shakeDirections = new List<Vector3>
            {
                new(0.1f, 0, 0.1f),
                new(0, 0, 0.1f),
                new(0.1f, 0, 0),
                new(0.1f, 0, 0.05f),
                new(0.05f, 0, 0.1f),
                new(0.05f, 0, 0.05f),
                new(0, 0, 0.05f),
                new(0.05f, 0, 0),
                new(-0.1f, 0, -0.1f),
                new(0.1f, 0, -0.1f),
                new(-0.1f, 0, 0.1f),
                new(0, 0, -0.1f),
                new(-0.1f, 0, 0),
                new(-0.1f, 0, 0.05f),
                new(0.1f, 0, -0.05f),
                new(-0.1f, 0, -0.05f),
                new(-0.05f, 0, -0.1f),
                new(0.05f, 0, -0.1f),
                new(-0.05f, 0, 0.1f),
                new(0.05f, 0, -0.05f),
                new(-0.05f, 0, 0.05f),
                new(0, 0, -0.05f),
                new(-0.05f, 0, 0),
            };
            _initialPosition = _rectTransform.localPosition;
            _currentShakeScale = _shakeScale;
        }

        private void StartShakingAnimation()
        {
            if (_currentShakeCoroutine != null)
            {
                StopCoroutine(_currentShakeCoroutine);
                _currentShakeCoroutine = null;
            }

            if (_secondsSinceDamage < SecondsWithoutDamageToResetShakeScale)
            {
                _currentShakeScale += _shakeScaleStep;
                if (_currentShakeScale > _shakeScaleMax)
                    _currentShakeScale = _shakeScaleMax;
            }
            else
                _currentShakeScale = _shakeScale;

            _currentShakeCoroutine = StartCoroutine(Shake());
        }

        private void Update()
        {
            _secondsSinceDamage += Time.deltaTime;
        }

        private void UpdateSeparators()
        {
            //TODO: REUSE SEPARATORS WHEN POSSIBLE
            if (_currentSeparators?.Any() == true)
                _currentSeparators.ForEach(Destroy);
            _currentSeparators = new List<GameObject>();

            var maxHealth = _health.MaxHealth;
            var separatorsCount = maxHealth / _healthCountBetweenSeparators;
            var separatorCountFloat = maxHealth / (float)_healthCountBetweenSeparators;
            var start = separatorCountFloat - separatorsCount;

            var max = _rectTransform.rect.max;
            var min = _rectTransform.rect.min;

            var maxX = max.x;
            var maxY = max.y;
            var minX = min.x;
            var minY = min.y;


            var height = maxY - minY;
            //var width = maxX - minX;

            //var centerX = maxX - width / 2;
            var centerY = maxY - height / 2;
            var leftCenterPoint = new Vector2(minX, centerY);
            var rightCenterPoint = new Vector2(maxX, centerY);
            var end = separatorsCount + start;
            for (var i = 1; i < end; i++)
            {
                var t = i / end;

                var lerp = Vector2.Lerp(leftCenterPoint, rightCenterPoint, t);
                var newSeparator = Instantiate(_separatorPrefab, transform);
                var separatorRect = newSeparator.GetComponent<RectTransform>();
                separatorRect.offsetMax = new Vector2(1.7f + lerp.x, separatorRect.offsetMax.y);
                _currentSeparators.Add(newSeparator);
            }
        }
    }
}
