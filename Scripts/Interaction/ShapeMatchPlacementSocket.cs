using UnityEngine;

namespace VRInteraction.SnapPlacement
{
    /// <summary>
    /// A SnapPlacementSocket variant that detects nearby matching objects
    /// with a box overlap instead of the base class's sphere overlap —
    /// useful when the socket's approach zone isn't naturally spherical
    /// (e.g. a shelf slot, a narrow tray, a shape-matching cutout where
    /// "nearby" should mean "roughly aligned," not just "within a radius").
    ///
    /// Demonstrates extending SnapPlacementSocket's virtual detection/
    /// gizmo methods rather than duplicating the placement, event, and
    /// highlight logic already provided by the base class.
    /// </summary>
    public class ShapeMatchPlacementSocket : SnapPlacementSocket
    {
        [Header("Box Detection")]
        [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 0.3f, 0.5f);
        [SerializeField] private Vector3 boxCenter = Vector3.zero;

        private SnapGrabbableObject currentNearbyObject;

        protected override void Update()
        {
            // Intentionally does not call base.Update() — detection here
            // uses a box overlap instead of the base class's sphere check.
            CheckNearbyObjects();
        }

        private void CheckNearbyObjects()
        {
            if (IsOccupied) return;

            Vector3 worldCenter = transform.position + transform.TransformDirection(boxCenter);
            Vector3 halfExtents = boxSize * 0.5f;
            Collider[] colliders = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);

            SnapGrabbableObject closest = null;
            float closestDistance = boxSize.magnitude;

            foreach (Collider col in colliders)
            {
                SnapGrabbableObject grabbable = col.GetComponent<SnapGrabbableObject>();
                if (grabbable == null) grabbable = col.GetComponentInParent<SnapGrabbableObject>();
                if (grabbable == null || !IsCorrectObjectPublic(grabbable)) continue;

                float distance = Vector3.Distance(transform.position, grabbable.transform.position);
                if (distance < closestDistance)
                {
                    closest = grabbable;
                    closestDistance = distance;
                }
            }

            currentNearbyObject = closest;
        }

        // Base class's IsCorrectObject is protected; this thin wrapper keeps
        // that encapsulation intact while still letting this subclass query it.
        private bool IsCorrectObjectPublic(SnapGrabbableObject obj) => IsCorrectPlacement(obj) || CanAcceptObject(obj);

        protected override void OnDrawGizmosSelected()
        {
            Vector3 localCenter = boxCenter;

            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localCenter, boxSize);

            Gizmos.color = IsOccupied ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(localCenter, boxSize * 0.6f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
