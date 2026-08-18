using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRInteraction.SnapPlacement
{
    [System.Serializable]
    public class SocketEvent : UnityEvent<SnapPlacementSocket> { }

    [System.Serializable]
    public class RejectionEvent : UnityEvent<string> { }

    /// <summary>
    /// An object that can be grabbed via XR Interaction Toolkit, carried
    /// around, and released near a SnapPlacementSocket. On release, it
    /// searches nearby colliders for the closest socket willing to accept
    /// it; if one is found within that socket's own snap distance, the
    /// object snaps into position and fires placement events. If not, it
    /// smoothly animates back to its starting position instead of just
    /// disappearing or falling through the floor.
    ///
    /// PHYSICS NOTE: the object is kept kinematic (no gravity, no physics
    /// simulation) for its entire lifecycle — grabbing, carrying, snapping,
    /// and returning are all handled by explicit transform movement, not
    /// Rigidbody forces. This is a deliberate choice, not an oversight: with
    /// several grabbable objects sitting close together on a surface, a
    /// non-kinematic Rigidbody can be knocked out of place by a nearby
    /// collision (e.g. picking up one object brushing another), which reads
    /// as a bug to anyone testing the scene. Keeping objects kinematic while
    /// idle removes that failure mode entirely, at the cost of not getting
    /// "real" physics behaviour if that's ever wanted instead.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SnapGrabbableObject : MonoBehaviour
    {
        [Header("Object Identity")]
        [SerializeField] private string objectID;
        [SerializeField] private string objectName;
        [SerializeField] private ObjectSize objectSize = ObjectSize.Medium;

        [Header("Snap Settings")]
        [Tooltip("World-space offset applied after snapping to a socket. Useful when an object's pivot isn't at its visual base.")]
        [SerializeField] private Vector3 snapOffset = Vector3.zero;

        [Header("Return-to-Origin")]
        [SerializeField] private float returnSpeed = 20f;

        [Header("Visual Feedback")]
        [SerializeField] private Material highlightMaterial;

        [Header("Events")]
        public UnityEvent  OnGrabbed;
        public UnityEvent  OnReleased;
        public SocketEvent OnPlacedCorrectly;
        public SocketEvent OnPlacedIncorrectly;

        /// <summary>Fires when the object is released near a socket cluster
        /// but fails to snap to any of them — useful for tracking near-miss
        /// attempts separately from a clean miss with no socket nearby.</summary>
        public RejectionEvent OnPlacementRejected;

        public enum ObjectSize { Short, Medium, Tall }

        public string ObjectID   => objectID;
        public string ObjectName => objectName;
        public ObjectSize Size   => objectSize;
        public bool IsGrabbed    => isGrabbed;
        public bool WasPlaced    => hasBeenPlaced;
        public SnapPlacementSocket CurrentSocket => currentSocket;

        private Vector3    originalPosition;
        private Quaternion originalRotation;
        private Transform  originPoint;
        private bool isGrabbed     = false;
        private bool isReturning   = false;
        private bool hasBeenPlaced = false;
        private SnapPlacementSocket currentSocket = null;
        private Renderer objectRenderer;
        private Material originalMaterial;
        private Rigidbody rb;
        private XRGrabInteractable grabInteractable;

        private void Awake()
        {
            rb             = GetComponent<Rigidbody>();
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer == null)
                objectRenderer = GetComponentInChildren<Renderer>();

            if (objectRenderer != null)
                originalMaterial = objectRenderer.material;

            originalPosition = transform.position;
            originalRotation = transform.rotation;

            // See class doc comment: idle objects are always kinematic.
            rb.isKinematic = true;
            rb.useGravity  = false;

            GameObject origin = new GameObject(gameObject.name + "_Origin");
            origin.transform.position = originalPosition;
            origin.transform.rotation = originalRotation;
            origin.transform.parent   = transform.parent;
            originPoint = origin.transform;

            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnXRGrab);
                grabInteractable.selectExited.AddListener(OnXRRelease);

                // Objects here are always kinematic, so the XRGrabInteractable's
                // "throw on detach" behaviour (which relies on Rigidbody
                // velocity) never applies — leaving it enabled just produces
                // a harmless but noisy warning on every release.
                grabInteractable.throwOnDetach = false;
            }

            if (OnPlacementRejected == null) OnPlacementRejected = new RejectionEvent();
        }

        private void Update()
        {
            if (isReturning && !isGrabbed)
                ReturnToOrigin();
        }

        private void OnXRGrab(SelectEnterEventArgs args)   => HandleGrab();
        private void OnXRRelease(SelectExitEventArgs args) => HandleRelease();

        public void HandleGrab()
        {
            isGrabbed   = true;
            isReturning = false;

            if (currentSocket != null)
            {
                currentSocket.RemoveObject();
                currentSocket = null;
            }

            if (highlightMaterial != null && objectRenderer != null)
                objectRenderer.material = highlightMaterial;

            OnGrabbed?.Invoke();
        }

        public void HandleRelease()
        {
            isGrabbed = false;

            SnapPlacementSocket nearbySocket = FindNearbySocketBySnapDistance();

            if (nearbySocket != null && nearbySocket.CanAcceptObject(this))
            {
                PlaceOnSocket(nearbySocket);
            }
            else
            {
                isReturning = true;

                bool wasNearAnySocket = IsNearAnySocket();
                if (wasNearAnySocket)
                    OnPlacementRejected?.Invoke(objectName);
            }

            if (originalMaterial != null && objectRenderer != null)
                objectRenderer.material = originalMaterial;

            OnReleased?.Invoke();
        }

        private bool IsNearAnySocket()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 1.0f);
            foreach (var col in colliders)
                if (col.GetComponent<SnapPlacementSocket>() != null)
                    return true;
            return false;
        }

        private SnapPlacementSocket FindNearbySocketBySnapDistance()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 2.0f);

            SnapPlacementSocket closestSocket   = null;
            float               closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                SnapPlacementSocket socket = collider.GetComponent<SnapPlacementSocket>();
                if (socket == null || !socket.CanAcceptObject(this)) continue;

                float distance = Vector3.Distance(transform.position, socket.GetSnapPosition());
                if (distance <= socket.SnapDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSocket   = socket;
                }
            }

            return closestSocket;
        }

        public void PlaceOnSocket(SnapPlacementSocket socket)
        {
            if (socket == null) return;

            currentSocket = socket;
            hasBeenPlaced = true;
            isReturning   = false;

            socket.OnCorrectPlacement.AddListener(RaiseCorrectPlacement);
            socket.OnIncorrectPlacement.AddListener(RaiseIncorrectPlacement);

            transform.position = socket.GetSnapPosition() + snapOffset;
            transform.rotation = socket.GetSnapRotation();

            socket.PlaceObject(this);

            socket.OnCorrectPlacement.RemoveListener(RaiseCorrectPlacement);
            socket.OnIncorrectPlacement.RemoveListener(RaiseIncorrectPlacement);
        }

        private void ReturnToOrigin()
        {
            if (originPoint == null) { isReturning = false; return; }

            float step = returnSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, originPoint.position, step);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, originPoint.rotation, step * 300f);

            if (Vector3.Distance(transform.position, originPoint.position) < 0.01f)
            {
                transform.position = originPoint.position;
                transform.rotation = originPoint.rotation;
                isReturning = false;
            }
        }

        private void RaiseCorrectPlacement()   => OnPlacedCorrectly?.Invoke(currentSocket);
        private void RaiseIncorrectPlacement() => OnPlacedIncorrectly?.Invoke(currentSocket);

        public void ResetToOrigin()
        {
            isReturning = false;
            if (originPoint != null)
            {
                transform.position = originPoint.position;
                transform.rotation = originPoint.rotation;
            }
            currentSocket = null;
            isGrabbed     = false;
        }

        public void OnPlacedInSocket(SnapPlacementSocket socket) { /* hook for subclasses / listeners */ }
        public void OnRemovedFromSocket() { /* hook for subclasses / listeners */ }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnXRGrab);
                grabInteractable.selectExited.RemoveListener(OnXRRelease);
            }
        }
    }
}
