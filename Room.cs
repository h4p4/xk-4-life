namespace RoomBattle.Rooms
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using RoomBattle.Common;
    using RoomBattle.Health;
    using RoomBattle.Health.Damage;
    using RoomBattle.Logging;
    using RoomBattle.Money;
    using RoomBattle.Roomers;
    using RoomBattle.Rooms.RoomVariants.Data;

    using UnityEngine;

    [RequireComponent(typeof(RoomMaterial))]
    [RequireComponent(typeof(RoomWallGroup), typeof(StandingPointGroup))]
    public abstract class Room : MonoBehaviour, IPurchasable, IStandingPointsContainer, IDamageable, IPausable
    {
        [SerializeField] private GameObject _model;
        [SerializeField] private GameObject _preview;
        private List<GameObject> _uiGameObjects;
        private List<RoomPreviewCopy> _previewCopies;
        private Renderer[] _previewRenderers;

        internal GameObject Model => _model;

        internal GameObject Preview => _preview;


        public Health Health { get; private set; }

        [field: SerializeField]
        internal RoomWallGroup Walls { get; private set; }

        public virtual bool TakeDamage(Damage damage, out string logMessage)
        {
            return Health.TakeDamage(damage, out logMessage);
        }

        public event EventHandler<ContainersSwappedEventArgs> ContainersSwapped;

        [field: SerializeField]
        public StandingPointGroup StandingPoints { get; private set; }

        protected virtual void Awake()
        {
            _previewCopies = new List<RoomPreviewCopy>();
            _previewRenderers = _preview.GetComponentsInChildren<Renderer>();
            _uiGameObjects = FindChildrenWithLayer(Model.gameObject, 5).ToList();

            foreach (var roomWall in Walls)
            {
                roomWall.EnableObstacle(IsObstacleEnabledByDefault);
            }
        }


        internal RoomPreviewCopy CreatePreviewCopy()
        {
            var instance = Instantiate(_preview, transform.position, transform.rotation);
            instance.layer = 2;
            DestroyImmediate(instance.GetComponent<RoomCollision>());
            DestroyImmediate(instance.GetComponent<Collider>());
            var previewCopy = new RoomPreviewCopy(instance, DestroyPreviewCopy);

            foreach (var uiObj in _uiGameObjects)
            {
                uiObj.transform.SetParent(previewCopy.RoomPreview.transform, false);
            }

            var renderers = instance.GetComponentsInChildren<Renderer>();
            SetPreviewActive(renderers, true);
            _previewCopies.Add(previewCopy);
            return previewCopy;
        }

        internal void NotifyContainersSwapped(ContainersSwappedEventArgs args)
        {
            ContainersSwapped?.Invoke(this, args);
        }

        private IEnumerable<GameObject> FindChildrenWithLayer(GameObject parentObject, int layer)
        {
            return parentObject.transform.Cast<Transform>()
                               .Where(childTransform => childTransform.gameObject.layer == layer)
                               .Select(x => x.gameObject);
        }

        private void DestroyPreviewCopy(GameObject obj)
        {
            _previewCopies.RemoveAll(x => x.RoomPreview == obj);
            if (!_previewCopies.Any())
                ReturnUiToThisRoom();
            else
            {
                var lastCopy = _previewCopies.Last();
                if (lastCopy.RoomPreview == null)
                    ReturnUiToThisRoom();
                else
                {
                    foreach (var uiObj in _uiGameObjects)
                    {
                        uiObj.transform.SetParent(lastCopy.RoomPreview.transform, false);
                    }
                }
            }
            Destroy(obj);
        }

        private void ReturnUiToThisRoom()
        {
            foreach (var uiObj in _uiGameObjects)
            {
                uiObj.transform.SetParent(transform, false);
            }
        }

        private void SetPreviewActive(Renderer[] renderers, bool b)
        {
            foreach (var child in renderers)
            {
                child.enabled = b;
            }
        }

        internal class RoomPreviewCopy
        {
            private readonly Action<GameObject> _destroyAction;
            private readonly GameObject _roomPreview;

            public RoomPreviewCopy(GameObject roomPreview, Action<GameObject> destroyAction)
            {
                _roomPreview = roomPreview;
                _destroyAction = destroyAction;
                IsAlive = true;
            }

            public GameObject RoomPreview => !IsAlive ? null : _roomPreview;

            public bool IsAlive { get; private set; }

            public void Destroy()
            {
                IsAlive = false;
                _destroyAction?.Invoke(_roomPreview);
            }
        }
    }
}
