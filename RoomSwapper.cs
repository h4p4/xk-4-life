// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace RoomBattle.Rooms
{
    using System;
    using System.Collections;

    using RoomBattle.Roomers;

    using Unity.VisualScripting;

    using UnityEngine;

    public class RoomSwapper : MonoBehaviour
    {
        [SerializeField] [SerializeReference] private SwapAnimationSettings _settings = new();

        private bool _isSwappingNow;
        private Room _firstRoom;
        private Room _secondRoom;
        private RoomSwapperAnimator _firstRoomAnimator;
        private RoomSwapperAnimator _secondRoomAnimator;

        public bool CanSwapRooms => _firstRoom != null && _secondRoom != null && !IsSwappingNow;

        public bool IsSwappingNow => _isSwappingNow;

        public Room FirstRoom => _firstRoom;

        public Room SecondRoom => _secondRoom;

        public bool TrySetFirstRoom(Room value)
        {
            var canSetValue = true;
            if (_secondRoom != null)
                canSetValue = value != _secondRoom && !IsSwappingNow;

            if (!canSetValue)
                return false;

            return SetRoomValue(value, ref _firstRoom, ref _firstRoomAnimator);
        }

        public bool TrySetSecondRoom(Room value)
        {
            var canSetValue = true;
            if (_firstRoom != null)
                canSetValue = value != _firstRoom && !IsSwappingNow;

            if (!canSetValue)
                return false;

            return SetRoomValue(value, ref _secondRoom, ref _secondRoomAnimator);
        }

        public void PreviewSwapRooms()
        {
            if (!CanSwapRooms)
                return;

            _firstRoomAnimator.StartPreviewSwapWith(_secondRoomAnimator);
            _secondRoomAnimator.StartPreviewSwapWith(_firstRoomAnimator);
        }

        public void SwapRooms()
        {
            if (!CanSwapRooms)
                return;

            _isSwappingNow = true;

            var isFirstRoomSwapEnded = false;
            var isSecondRoomSwapEnded = false;

            _firstRoomAnimator.SwapEnded += FirstRoomSwapEnded;
            _secondRoomAnimator.SwapEnded += SecondRoomSwapEnded;

            _firstRoomAnimator.ApplyCurrentPreviewSwap();
            _secondRoomAnimator.ApplyCurrentPreviewSwap();

            void FirstRoomSwapEnded()
            {
                isFirstRoomSwapEnded = true;
                if (isSecondRoomSwapEnded)
                    EndSwap();
            }

            void SecondRoomSwapEnded()
            {
                isSecondRoomSwapEnded = true;
                if (isFirstRoomSwapEnded)
                    EndSwap();
            }

            void EndSwap()
            {
                SwapCurrentRooms();
                _isSwappingNow = false;
                _firstRoomAnimator.SwapEnded -= FirstRoomSwapEnded;
                _secondRoomAnimator.SwapEnded -= SecondRoomSwapEnded;
                _firstRoom = null;
                _secondRoom = null;
                _firstRoomAnimator = null;
                _secondRoomAnimator = null;
            }
        }

        private bool SetRoomValue(Room newValue, ref Room roomField, ref RoomSwapperAnimator roomSwapperAnimator)
        {
            if (newValue == roomField && newValue != null)
                return false;

            roomSwapperAnimator?.StopSwapPreview();
            roomField = newValue;
            if (roomField == null)
                return true;

            roomSwapperAnimator = new RoomSwapperAnimator(roomField, _settings);
            roomSwapperAnimator.StartSwapPreview();
            return true;
        }

        private void SwapCurrentRooms()
        {
            if (_firstRoom.Walls.Count != _secondRoom.Walls.Count)
                throw new Exception("Rooms must have the same wall count!");

            // ReSharper disable once SwapViaDeconstruction
            var firstRoomPosition = _firstRoom.transform.position;
            _firstRoom.transform.position = _secondRoom.transform.position;
            _secondRoom.transform.position = firstRoomPosition;

            for (var i = 0; i < _firstRoom.Walls.Count; i++)
            {
                var firstRoomWall = _firstRoom.Walls[i];
                var secondRoomWall = _secondRoom.Walls[i];

                if (firstRoomWall.IsGateOpen)
                {
                    if (secondRoomWall.IsGateOpen)
                        continue;

                    secondRoomWall.OpenGate();
                    firstRoomWall.CloseGate();
                }
                else
                {
                    if (!secondRoomWall.IsGateOpen)
                        continue;

                    secondRoomWall.CloseGate();
                    firstRoomWall.OpenGate();
                }
            }

            var args = new ContainersSwappedEventArgs(_firstRoom, _secondRoom);
            _secondRoom.NotifyContainersSwapped(args);
            _firstRoom.NotifyContainersSwapped(args);
        }

        private interface IRoomCopyContainer
        {
            Room Room { get; }

            Room.RoomPreviewCopy RoomCopy { get; }
        }

        [Serializable]
        public class SwapAnimationSettings
        {
            [SerializeField] private AnimationCurve _swapAnimationCurve;
            [SerializeField] private AnimationCurve _swapAnimationShortCurve;
            [SerializeField] private float _swapDuration = 0.6f;
            [SerializeField] private float _swapShortDuration = 0.3f;
            [SerializeField] private Vector3 _swapOffset = new(0, 3f, 0);
            [SerializeField] private AudioClip _swapPreviewClip;
            [SerializeField] private AudioSource _audioSource;

            public AnimationCurve SwapAnimationCurve => _swapAnimationCurve;
            public AnimationCurve SwapAnimationShortCurve => _swapAnimationShortCurve;

            public float SwapDuration => _swapDuration;
            public float SwapShortDuration => _swapShortDuration;

            public Vector3 SwapOffset => _swapOffset;

            public AudioClip SwapPreviewClip => _swapPreviewClip;

            public AudioSource AudioSource => _audioSource;
        }

        private class RoomSwapperAnimator : IRoomCopyContainer
        {
            private readonly Room _room;
            private readonly SwapAnimationSettings _settings;
            private Coroutine _previewSwapWithCoroutine;
            private Coroutine _startSwapPreviewCoroutine;
            private CoroutineRunner _runner;
            private IRoomCopyContainer _otherCopyContainer;
            private RoomPreviewCopyContainer _roomCopyContainer;
            private Coroutine _liftRoomDownCoroutine;

            public RoomSwapperAnimator(Room room, SwapAnimationSettings settings)
            {
                _room = room;
                _settings = settings;
                _runner = CoroutineRunner.instance;
            }

            private AnimationCurve AnimationCurve => _settings.SwapAnimationCurve;
            
            private AnimationCurve AnimationCurveShort => _settings.SwapAnimationShortCurve;

            private float Duration => _settings.SwapDuration;
            
            private float ShortDuration => _settings.SwapShortDuration;

            private Vector3 Offset => _settings.SwapOffset;

            public Room Room => _room;

            public Room.RoomPreviewCopy RoomCopy => _roomCopyContainer.Copy;

            public event Action SwapEnded;

            public void ApplyCurrentPreviewSwap()
            {
                if (_otherCopyContainer == null)
                    return;

                if (_startSwapPreviewCoroutine != null)
                {
                    _runner.StopCoroutine(_startSwapPreviewCoroutine);
                    _startSwapPreviewCoroutine = null;
                }

                if (_previewSwapWithCoroutine != null)
                {
                    _runner.StopCoroutine(_previewSwapWithCoroutine);
                    _previewSwapWithCoroutine = null;
                }

                _runner.StartCoroutine(StartApplyCurrentPreviewSwap());
            }

            public void StartPreviewSwapWith(IRoomCopyContainer copy)
            {
                _otherCopyContainer = copy;
                if (_startSwapPreviewCoroutine != null)
                {
                    _runner.StopCoroutine(_startSwapPreviewCoroutine);
                    _startSwapPreviewCoroutine = null;
                }

                if (_previewSwapWithCoroutine != null)
                {
                    _runner.StopCoroutine(_previewSwapWithCoroutine);
                    _previewSwapWithCoroutine = null;
                }

                _settings.AudioSource.PlayOneShot(_settings.SwapPreviewClip);
                _previewSwapWithCoroutine = _runner.StartCoroutine(PreviewSwapWith(copy));
            }

            public void StartSwapPreview()
            {
                _roomCopyContainer ??= new RoomPreviewCopyContainer(_room.CreatePreviewCopy());
                _startSwapPreviewCoroutine = _runner.StartCoroutine(LiftRoomUpAndSetCoroutineToNull());

                IEnumerator LiftRoomUpAndSetCoroutineToNull()
                {
                    yield return LiftRoomUp(_room.transform.position, _roomCopyContainer);
                    _startSwapPreviewCoroutine = null;
                }
            }

            public void StopSwapPreview()
            {
                if (_startSwapPreviewCoroutine != null)
                {
                    _runner.StopCoroutine(_startSwapPreviewCoroutine);
                    _startSwapPreviewCoroutine = null;
                }

                if (_previewSwapWithCoroutine != null)
                {
                    _runner.StopCoroutine(_previewSwapWithCoroutine);
                    _previewSwapWithCoroutine = null;
                }
                if (_liftRoomDownCoroutine != null)
                {
                    _runner.StopCoroutine(_liftRoomDownCoroutine);
                    _liftRoomDownCoroutine = null;
                }

                _roomCopyContainer ??= new RoomPreviewCopyContainer(_room.CreatePreviewCopy());
                _liftRoomDownCoroutine = _runner.StartCoroutine(LiftRoomDown(_room.transform.position, _roomCopyContainer));
            }

            private IEnumerator LiftRoomDown(Vector3 roomPosition, RoomPreviewCopyContainer copyContainer,
                Action callback = null)
            {
                if (copyContainer.Copy == null)
                    yield break;
                    
                var roomPreviewTransform = copyContainer.Copy.RoomPreview.transform;

                var animDurationCurrent = 0f;
                var currentRoomPreviewPos = roomPreviewTransform.position;
                while (animDurationCurrent < Duration)
                {
                    if (copyContainer.Copy?.IsAlive != true)
                    {
                        copyContainer.Copy = null;
                        callback?.Invoke();
                        yield break;
                    }

                    animDurationCurrent += Time.deltaTime;
                    var t = animDurationCurrent / Duration;
                    var tAnimated = AnimationCurve.Evaluate(t);
                    var newPos = Vector3.LerpUnclamped(currentRoomPreviewPos, roomPosition, tAnimated);
                    copyContainer.Copy.RoomPreview.transform.position = newPos;
                    yield return null;
                }

                copyContainer.Copy?.Destroy();
                copyContainer.Copy = null;
                callback?.Invoke();
            }
            private IEnumerator LiftRoomDownFast(Vector3 roomPosition, RoomPreviewCopyContainer copyContainer,
                Action callback)
            {
                if (copyContainer.Copy == null)
                    yield break;
                    
                var roomPreviewTransform = copyContainer.Copy.RoomPreview.transform;

                var animDurationCurrent = 0f;
                var currentRoomPreviewPos = roomPreviewTransform.position;
                while (animDurationCurrent < ShortDuration)
                {
                    if (copyContainer.Copy?.IsAlive != true)
                    {
                        copyContainer.Copy = null;
                        callback?.Invoke();
                        yield break;
                    }

                    animDurationCurrent += Time.deltaTime;
                    var t = animDurationCurrent / ShortDuration;
                    var tAnimated = AnimationCurveShort.Evaluate(t);
                    var newPos = Vector3.LerpUnclamped(currentRoomPreviewPos, roomPosition, tAnimated);
                    copyContainer.Copy.RoomPreview.transform.position = newPos;
                    yield return null;
                }

                copyContainer.Copy?.Destroy();
                copyContainer.Copy = null;
                callback?.Invoke();
            }

            private IEnumerator LiftRoomUp(Vector3 roomPosition, RoomPreviewCopyContainer copyContainer)
            {
                if (copyContainer.Copy == null)
                    yield break;

                var targetPos = roomPosition + Offset;
                var previewRoomTransform = copyContainer.Copy.RoomPreview.transform;
                var animDurationCurrent = 0f;
                var currentRoomPreviewPos = previewRoomTransform.position;

                while (animDurationCurrent < ShortDuration)
                {
                    if (copyContainer.Copy?.IsAlive != true)
                    {
                        copyContainer.Copy = null;
                        yield break;
                    }

                    animDurationCurrent += Time.deltaTime;
                    var t = animDurationCurrent / ShortDuration;
                    var tAnimated = AnimationCurve.Evaluate(t);
                    var newPos = Vector3.LerpUnclamped(currentRoomPreviewPos, targetPos, tAnimated);
                    copyContainer.Copy.RoomPreview.transform.position = newPos;
                    yield return null;
                }
            }

            private IEnumerator PreviewSwapWith(IRoomCopyContainer roomCopyContainer)
            {
                if (_roomCopyContainer.Copy == null)
                    yield break;

                var otherRoomCopy = roomCopyContainer.RoomCopy;
                var otherRoomPos = roomCopyContainer.Room.transform.position;
                var targetPos = otherRoomPos + Offset;

                var currentRoomPreviewPos = _roomCopyContainer.Copy.RoomPreview.transform.position;
                var animDurationCurrent = 0f;
                while (animDurationCurrent < Duration)
                {
                    if (_roomCopyContainer.Copy.RoomPreview == null || otherRoomCopy.RoomPreview == null)
                        yield break;
                    if (_roomCopyContainer.Copy?.IsAlive != true || otherRoomCopy.IsAlive != true)
                        yield break;

                    animDurationCurrent += Time.deltaTime;
                    var t = animDurationCurrent / Duration;
                    var tAnimated = AnimationCurve.Evaluate(t);
                    var newPos = Vector3.LerpUnclamped(currentRoomPreviewPos, targetPos, tAnimated);
                    _roomCopyContainer.Copy.RoomPreview.transform.position = newPos;
                    yield return null;
                }
            }

            private IEnumerator StartApplyCurrentPreviewSwap()
            {
                var otherRoomPos = _otherCopyContainer.Room.transform.position;
                var routine = SwapEnded == null
                    ? LiftRoomDown(otherRoomPos, _roomCopyContainer)
                    : LiftRoomDownFast(otherRoomPos, _roomCopyContainer, SwapEnded.Invoke);
                yield return routine;
            }

            private class RoomPreviewCopyContainer
            {
                public RoomPreviewCopyContainer(Room.RoomPreviewCopy copy)
                {
                    Copy = copy;
                }

                public Room.RoomPreviewCopy Copy { get; set; }
            }
        }
    }
}
