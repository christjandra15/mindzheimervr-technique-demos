using UnityEngine;
using UnityEngine.Events;

namespace VRInteraction.SnapPlacement
{
    [System.Serializable]
    public class PlacementEvent : UnityEvent { }

    [System.Serializable]
    public class ObjectPlacementEvent : UnityEvent<SnapGrabbableObject> { }

    /// <summary>
    /// A placement target that accepts a specific grabbable object (by ID or
    /// size category), shows a highlight when a matching object is held
    /// nearby, and snaps the object into position on release. Fires distinct
    /// correct/incorrect events so a caller can drive scoring, audio,
    /// haptics, or any other feedback without this class knowing about any
    /// of it.
    ///
    /// Designed to be subclassed for different acceptance/detection
    /// strategies — see ShapeMatchPlacementSocket for a box-overlap-based
    /// variant that detects nearby objects by physical proximity rather
    /// than the sphere-cast approach used here.
    /// </summary>
    public class SnapPlacementSocket : MonoBehaviour
    {
        [Header("Socket Settings")]
        [SerializeField] private Transform placementPosition;
        [SerializeField] private SnapGrabbableObject.ObjectSize acceptedSize;
        [SerializeField] protected string acceptedObjectID;

        [Header("Visual Feedback")]
        [SerializeField] protected GameObject silhouetteVisual;
        [SerializeField] protected Material silhouetteMaterial;
        [SerializeField] protected Material highlightMaterial;
        [SerializeField] protected float highlightDistance = 0.5f;
        [SerializeField] protected float snapDistance = 0.3f;

        [Header("Events")]
        public PlacementEvent OnCorrectPlacement;
        public PlacementEvent OnIncorrectPlacement;
        public ObjectPlacementEvent OnObjectPlaced;
        public ObjectPlacementEvent OnObjectRemoved;

        protected SnapGrabbableObject currentObject;
        protected Renderer silhouetteRenderer;
        protected bool isHighlighted = false;

        public bool IsOccupied => currentObject != null;
        public SnapGrabbableObject CurrentObject => currentObject;
        public string AcceptedObjectID => acceptedObjectID;

        /// <summary>Exposes the snap distance so SnapGrabbableObject can use
        /// each socket's own threshold when deciding whether to snap on
        /// release, rather than a single hard-coded global distance.</summary>
        public float SnapDistance => snapDistance;

        protected virtual void Awake()
        {
            if (placementPosition == null)
                placementPosition = transform;

            if (silhouetteVisual != null)
            {
                silhouetteRenderer = silhouetteVisual.GetComponentInChildren<Renderer>();

                if (silhouetteMaterial != null)
                    foreach (var r in silhouetteVisual.GetComponentsInChildren<Renderer>())
                        r.material = silhouetteMaterial;

                silhouetteVisual.SetActive(true);
            }

            if (OnCorrectPlacement == null)   OnCorrectPlacement   = new PlacementEvent();
            if (OnIncorrectPlacement == null) OnIncorrectPlacement = new PlacementEvent();
            if (OnObjectPlaced == null)       OnObjectPlaced       = new ObjectPlacementEvent();
            if (OnObjectRemoved == null)      OnObjectRemoved      = new ObjectPlacementEvent();
        }

        protected virtual void Start() { }

        protected virtual void Update()
        {
            if (IsOccupied || silhouetteVisual == null) return;
            UpdateHighlight();
        }

        protected void UpdateHighlight()
        {
            SnapGrabbableObject nearbyObject = FindNearbyCorrectObject();

            if (nearbyObject != null && !isHighlighted)
            {
                isHighlighted = true;
                SetSilhouetteMaterial(highlightMaterial);
            }
            else if (nearbyObject == null && isHighlighted)
            {
                isHighlighted = false;
                SetSilhouetteMaterial(silhouetteMaterial);
            }
        }

        private void SetSilhouetteMaterial(Material mat)
        {
            if (silhouetteVisual == null || mat == null) return;
            foreach (var r in silhouetteVisual.GetComponentsInChildren<Renderer>())
                r.material = mat;
        }

        protected SnapGrabbableObject FindNearbyCorrectObject()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, highlightDistance);

            foreach (var collider in colliders)
            {
                SnapGrabbableObject obj = collider.GetComponent<SnapGrabbableObject>();
                if (obj != null && obj.IsGrabbed && IsCorrectObject(obj))
                    return obj;
            }

            return null;
        }

        public virtual bool CanAcceptObject(SnapGrabbableObject obj)
        {
            if (IsOccupied) return false;
            if (obj == null) return false;

            if (!string.IsNullOrEmpty(acceptedObjectID))
                return obj.ObjectID == acceptedObjectID;

            return obj.Size == acceptedSize;
        }

        protected virtual bool IsCorrectObject(SnapGrabbableObject obj)
        {
            if (!string.IsNullOrEmpty(acceptedObjectID))
                return obj.ObjectID == acceptedObjectID;

            return obj.Size == acceptedSize;
        }

        public virtual bool IsCorrectPlacement(SnapGrabbableObject obj)
        {
            if (obj == null) return false;
            return IsCorrectObject(obj);
        }

        public virtual void PlaceObject(SnapGrabbableObject obj)
        {
            if (obj == null || IsOccupied) return;

            currentObject = obj;
            currentObject.OnPlacedInSocket(this);

            if (silhouetteVisual != null)
                silhouetteVisual.SetActive(false);

            bool isCorrect = IsCorrectPlacement(obj);

            if (isCorrect)
            {
                Debug.Log($"[SnapPlacementSocket] Correct placement: {obj.ObjectName} -> {gameObject.name}");
                OnCorrectPlacement?.Invoke();
            }
            else
            {
                Debug.Log($"[SnapPlacementSocket] Incorrect placement: {obj.ObjectName} -> {gameObject.name} " +
                          $"(expected: {acceptedObjectID})");
                OnIncorrectPlacement?.Invoke();
            }

            OnObjectPlaced?.Invoke(obj);
        }

        public virtual void RemoveObject()
        {
            if (currentObject == null) return;

            SnapGrabbableObject removedObject = currentObject;
            currentObject.OnRemovedFromSocket();
            currentObject = null;

            if (silhouetteVisual != null)
            {
                silhouetteVisual.SetActive(true);
                SetSilhouetteMaterial(silhouetteMaterial);
            }

            isHighlighted = false;
            OnObjectRemoved?.Invoke(removedObject);
        }

        public virtual Vector3 GetSnapPosition() => placementPosition.position;
        public virtual Quaternion GetSnapRotation() => placementPosition.rotation;

        public void SetAcceptedSize(SnapGrabbableObject.ObjectSize size) => acceptedSize = size;
        public void SetAcceptedObjectID(string id) => acceptedObjectID = id;

        public virtual void ShowSilhouette(bool show)
        {
            if (silhouetteVisual != null)
                silhouetteVisual.SetActive(show);
        }

        public virtual void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            SetSilhouetteMaterial(highlight ? highlightMaterial : silhouetteMaterial);
        }

        public virtual void Clear() => RemoveObject();

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, snapDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, highlightDistance);
        }
    }
}
